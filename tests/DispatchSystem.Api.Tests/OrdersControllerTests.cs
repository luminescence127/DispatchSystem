using DispatchSystem.Api.Data;
using DispatchSystem.Api.Dtos;
using DispatchSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DispatchSystem.Api.Tests
{
    // IClassFixture<DispatchApiFactory>：跟 xUnit 說「這個類別的測試要用那台測試伺服器」。
    // xUnit 會自己建一個 DispatchApiFactory（並呼叫它的 InitializeAsync 起容器），
    // 然後從主建構子的 factory 參數塞給你。
    public class OrdersControllerTests(DispatchApiFactory factory) : IClassFixture<DispatchApiFactory>, IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await DeleteAllOrdersAsync();
        }
        public Task DisposeAsync() => Task.CompletedTask;
        // [Fact] = 「這是一個測試」。沒有它，xUnit 根本不會執行這個方法。
        [Fact]
        public async Task GetOrders_FilterByStatus_ReturnsOnlyThatStatus()
        {
            var client = factory.CreateClient();

            var createdId = await CreateOrderAsync(client, "篩選-未指派");
            var assignedId = await CreateOrderAsync(client, "篩選-已指派");

            var assignRes = await client.PostAsync($"/api/orders/{assignedId}/assign", null);
            assignRes.EnsureSuccessStatusCode();

            var body = await GetOrderListAsync(client, "/api/orders?status=Created");

            Assert.Equal(1, body.TotalCount);

            var item = Assert.Single(body.Items);

            Assert.Equal(createdId, item.Id);
            Assert.Equal(OrderStatus.Created, item.Status);
        }
        [Fact]
        public async Task GetOrders_SplitsResultsIntoPages_NewestFirst()
        {
            var client = factory.CreateClient();

            var firstCreatedId = await CreateOrderAsync(client, "分頁-1");
            var secondCreatedId = await CreateOrderAsync(client, "分頁-2");
            var thirdCreatedId = await CreateOrderAsync(client, "分頁-3");

            var page1 = await GetOrderListAsync(client, "/api/orders?page=1&pageSize=2");
            var page2 = await GetOrderListAsync(client, "/api/orders?page=2&pageSize=2");

            Assert.Equal(3, page1.TotalCount);
            Assert.Equal(new[] { thirdCreatedId, secondCreatedId }, page1.Items.Select(item => item.Id));
            Assert.Equal(new[] { firstCreatedId }, page2.Items.Select(item => item.Id));
        }
        [Fact]
        public async Task GetOrders_WhenPageSizeIsTooBig_ReturnsAtMost100Orders()
        {
            var client = factory.CreateClient();
            await InsertOrdersDirectlyAsync(101);

            var body = await GetOrderListAsync(client, "/api/orders?pageSize=1000");

            Assert.Equal(101, body.TotalCount);
            Assert.Equal(100, body.PageSize);
            Assert.Equal(100, body.Items.Count);
        }
        [Fact]
        public async Task GetOrders_WhenStatusIsNotAKnownValue_Returns400()
        {
            var client = factory.CreateClient();

            var res = await client.GetAsync("/api/orders?status=Foo");

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
        [Fact]
        public async Task GetOrderById_Returns200()
        {
            var client = factory.CreateClient();

            // 準備：自己建一張單，拿回它的 id
            var id = await CreateOrderAsync(client, "測試查詢訂單");

            // 動作：用 GET 打查詢端點
            var res = await client.GetAsync($"/api/orders/{id}");

            // 檢查 1：狀態碼
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            // 檢查 2：拆信封，把 body 讀成 Order
            var order = await res.Content.ReadFromJsonAsync<Order>(JsonOptions);

            Assert.NotNull(order);
            Assert.Equal(id, order.Id);
            Assert.Equal("測試查詢訂單", order.CustomerName);
            Assert.Equal(OrderStatus.Created, order.Status);
        }
        [Fact]
        public async Task GetOrderById_WhenNotExists_Returns404()
        {
            var client = factory.CreateClient();

            var res = await client.GetAsync("/api/orders/999999");

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
        [Fact]
        // 回傳 Task 就好，不要 Task<ActionResult>。
        // ActionResult 是「伺服器那一邊」controller 在用的東西；
        // 測試站在「客戶端」，拿到的是 HttpResponseMessage。兩邊不一樣。
        // 測試方法本來就不回傳東西給誰看，xUnit 只看它有沒有丟例外。
        public async Task CreateOrder_SavesOrderWithCreatedStatus()
        {
            // 拿一個能打這台測試伺服器的 HttpClient（跟前兩支測試一樣）
            var client = factory.CreateClient();

            // ── 動作：打下單端點 ──────────────────────────────────
            // PostAsJsonAsync 幫做三件事：物件轉 JSON、設好 Content-Type、送出。
            //
            // new { ... } 是「匿名物件」＝沒有名字的臨時物件，
            // 等同 JS 的 { customerName: "王小明", ... }。
            // 這裡的屬性名就會變成 JSON 的欄位名，對到 CreateOrderRequest 的三個欄位。
            var res = await client.PostAsJsonAsync("/api/orders", new
            {
                customerName = "王小明",
                pickupAddress = "台北市中正區重慶南路一段122號",
                dropoffAddress = "台北市信義區市府路1號",
            });

            // ── 檢查 1：HTTP 回應是 201 Created ──────────────────
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);

            // ── 檢查 2：資料真的進資料庫了 ────────────────────────
            // 這一段是之前漏掉 SaveChangesAsync 學到的規則：
            // 光看 API 回 201 會被騙，一定要回頭查一次資料庫。

            // 開一個 scope 才拿得到 DbContext（DbContext 是 Scoped，
            // 平常一個 HTTP 請求配一個，現在不在請求裡所以要自己開）。
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            // AsNoTracking()：只讀不改，叫 EF 不用追蹤變更。
            // SingleAsync()：「剛好找到一筆」——找到 0 筆或 2 筆以上都會丟例外，
            //                所以它順便幫斷言了「只寫進去一筆」。
            var order = await db.Orders
                .AsNoTracking()
                .SingleAsync(o => o.CustomerName == "王小明");

            // 存進去的狀態是 Created、還沒指派外送員
            Assert.Equal(OrderStatus.Created, order.Status);
            Assert.Null(order.RiderId);
        }
        [Fact]
        public async Task AssignOrder_SetsStatusAssignedAndPicksAvailableRider()
        {
            var client = factory.CreateClient();

            //創建訂單
            var id = await CreateOrderAsync(client, "測試指派訂單");

            //指派訂單
            var res = await client.PostAsync($"/api/orders/{id}/assign", null);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var order = await GetOrderFromDbAsync(id);

            Assert.Equal(OrderStatus.Assigned, order.Status);
            Assert.Equal(1, order.RiderId);
        }
        [Fact]
        public async Task AcceptOrder_SetsStatusAccepted()
        {
            var client = factory.CreateClient();

            //創建訂單
            var id = await CreateOrderAsync(client, "測試接受訂單");

            //指派訂單
            var assignedRes = await client.PostAsync($"/api/orders/{id}/assign", null);
            assignedRes.EnsureSuccessStatusCode();

            //接受訂單
            var acceptedRes = await client.PostAsync($"/api/orders/{id}/accept", null);
            Assert.Equal(HttpStatusCode.OK, acceptedRes.StatusCode);

            //反查資料庫
            var order = await GetOrderFromDbAsync(id);
            Assert.Equal(OrderStatus.Accepted, order.Status);
        }
        [Fact]
        public async Task CompleteOrder_SetsStatusCompleted()
        {
            var client = factory.CreateClient();

            //創建訂單
            var id = await CreateOrderAsync(client, "測試完成訂單");

            //指派訂單
            var assignedRes = await client.PostAsync($"/api/orders/{id}/assign", null);
            assignedRes.EnsureSuccessStatusCode();

            //接受訂單
            var acceptedRes = await client.PostAsync($"/api/orders/{id}/accept", null);
            acceptedRes.EnsureSuccessStatusCode();

            //完成訂單
            var completedRes = await client.PostAsync($"/api/orders/{id}/complete", null);
            Assert.Equal(HttpStatusCode.OK, completedRes.StatusCode);

            //反查資料庫
            var order = await GetOrderFromDbAsync(id);
            Assert.Equal(OrderStatus.Completed, order.Status);
        }
        [Fact]
        public async Task CompleteOrderFromCreatedStatus_Returns409()
        {
            var client = factory.CreateClient();

            //創建訂單
            var id = await CreateOrderAsync(client, "測試無法從創建狀態完成訂單");

            //完成訂單
            var res = await client.PostAsync($"/api/orders/{id}/complete", null);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

            //回查資料庫 狀態仍為 Created
            var order = await GetOrderFromDbAsync(id);
            Assert.Equal(OrderStatus.Created, order.Status);
        }
        [Fact]
        public async Task AssignOrderNotExisting_Returns404()
        {
            var client = factory.CreateClient();

            //指派不存在的訂單
            var res = await client.PostAsync($"/api/orders/999999/assign", null);
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
        [Fact]
        public async Task AssignOrder_PicksTheAvailableRider_NotTheFirstRow()
        {
            var client = factory.CreateClient();
            var id = await CreateOrderAsync(client, "測試挑可用外送員");

            await SetRiderAvailabilityAsync(1, false);
            await SetRiderAvailabilityAsync(2, true);

            try
            {
                var res = await client.PostAsync($"/api/orders/{id}/assign", null);

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                var order = await GetOrderFromDbAsync(id);

                Assert.Equal(OrderStatus.Assigned, order.Status);
                Assert.Equal(2, order.RiderId);
            }
            finally
            {
                await SetRiderAvailabilityAsync(1, true);
                await SetRiderAvailabilityAsync(2, false);
            }
        }
        [Fact]
        public async Task AssignOrder_WhenNoRiderIsAvailable_Returns409()
        {
            var client = factory.CreateClient();
            var id = await CreateOrderAsync(client, "測試沒有可用外送員");

            await SetRiderAvailabilityAsync(1, false);

            try
            {
                var res = await client.PostAsync($"/api/orders/{id}/assign", null);

                Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

                var order = await GetOrderFromDbAsync(id);

                Assert.Equal(OrderStatus.Created, order.Status);
                Assert.Null(order.RiderId);
            }
            finally
            {
                await SetRiderAvailabilityAsync(1, true);
            }
        }
        [Fact]
        public async Task ClaimOrder_SetsStatusAcceptedAndRecordsTheRider()
        {
            var client = factory.CreateClient();
            var id = await CreateOrderAsync(client, "測試搶單");

            await SetRidersAvailabilityAsync(new[] { 3 }, true);

            try
            {
                var res = await client.PostAsJsonAsync($"/api/orders/{id}/claim", new { riderId = 3 });

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                var order = await GetOrderFromDbAsync(id);

                Assert.Equal(OrderStatus.Accepted, order.Status);
                Assert.Equal(3, order.RiderId);
            }
            finally
            {
                await SetRidersAvailabilityAsync(new[] { 3 }, false);
            }
        }
        [Fact]
        public async Task ClaimOrder_WhenRiderIsNotAvailable_Returns409()
        {
            var client = factory.CreateClient();
            var id = await CreateOrderAsync(client, "測試離線外送員搶單");

            //3 號在種子資料裡就是不可接單，這裡不需要動它
            var res = await client.PostAsJsonAsync($"/api/orders/{id}/claim", new { riderId = 3 });

            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

            var order = await GetOrderFromDbAsync(id);

            Assert.Equal(OrderStatus.Created, order.Status);
            Assert.Null(order.RiderId);
        }
        [Fact]
        public async Task ClaimOrder_WhenTwoRidersReadTheSameOrder_TheSecondSaveIsRejected()
        {
            var client = factory.CreateClient();
            var id = await CreateOrderAsync(client, "測試樂觀鎖擋得住");//兩個人都看到空位、同時伸手，慢的那隻手被打掉

            using var scopeA = factory.Services.CreateScope();
            using var scopeB = factory.Services.CreateScope();

            var dbA = scopeA.ServiceProvider.GetRequiredService<DispatchDbContext>();
            var dbB = scopeB.ServiceProvider.GetRequiredService<DispatchDbContext>();

            var orderA = await dbA.Orders.SingleAsync(o => o.Id == id);
            var orderB = await dbB.Orders.SingleAsync(o => o.Id == id);

            orderA.RiderId = 3;
            orderA.Status = OrderStatus.Accepted;
            await dbA.SaveChangesAsync();

            orderB.RiderId = 4;
            orderB.Status = OrderStatus.Accepted;

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());

            var order = await GetOrderFromDbAsync(id);

            Assert.Equal(3, order.RiderId);
        }
        [Fact]
        public async Task ClaimOrder_WhenManyRidersClaimAtOnce_OnlyOneWins()
        {
            var client = factory.CreateClient();
            var id = await CreateOrderAsync(client, "測試併發搶單");

            var riderIds = new[] { 3, 4, 5, 6, 7, 8, 9, 10 };

            await SetRidersAvailabilityAsync(riderIds, true);

            try
            {
                // ┌─ 這一段在做什麼 ────────────────────────────────────────┐
                // │ 讓八個搶單請求「盡量同時」送出去。                       │
                // │ 比喻：gate 是發令槍，八個外送員是選手。                  │
                // └────────────────────────────────────────────────────────┘

                // 發令槍。它身上的 gate.Task 代表「槍響」這件事，
                // 而且它永遠不會自己響——只有 SetResult() 才會讓它響。
                var gate = new TaskCompletionSource();

                var calls = riderIds
                    // Select = 把陣列裡每個數字換成別的東西。
                    // 這裡換成「一個正在進行中的工作」（Task），
                    // 就像叫八個人去跑腿，手上拿到的是八張號碼牌、不是八個便當。
                    .Select(async riderId =>
                    {
                        // 每個選手的第一件事：蹲在起跑線上聽槍聲。
                        // 槍還沒響，所以跑到這一行就停住，一個請求都還沒送出去。
                        await gate.Task;

                        // 槍響之後才會執行到這裡：送出搶單請求。
                        return await client.PostAsJsonAsync($"/api/orders/{id}/claim", new { riderId });
                    })
                    // Select 有個規矩：沒人跟它要結果，它就不動（延遲執行）。
                    // ToArray() = 我現在就要 —— 這一行才真的讓八個選手上場，
                    // 然後八個全部蹲在上面那行等槍聲。
                    .ToArray();

                // 砰。八個同時起跑。
                gate.SetResult();

                // 等八個都跑完，拿回八個 HTTP 回應。
                var responses = await Task.WhenAll(calls);

                // Assert.Single 一次做兩件事：斷言「剛好一個」，順便把那一個交出來。
                //
                // 條件要直接交給它，不要寫成 Assert.Single(responses.Where(...))。
                // ── xUnit2031 是什麼 ──
                // 裝 xUnit 的時候會一起裝一組「程式碼檢查規則」，每次 dotnet build
                // 都會自動掃測試程式碼，違反就出警告，編號長這樣：xUnit2031。
                // 這條的標題是 Do not use Where clause with Assert.Single，
                // 官方理由：Assert.Single 本來就有一個可以直接吃條件的版本，
                // 用它比較簡潔，也更看得出意圖。
                // 規則全文：https://xunit.net/xunit.analyzers/rules/xUnit2031
                var winner = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
                Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

                // 拆開贏家那份回應的 body，看 API 說是誰搶到的。
                var winnerOrder = await winner.Content.ReadFromJsonAsync<Order>(JsonOptions);

                Assert.NotNull(winnerOrder);

                var order = await GetOrderFromDbAsync(id);

                Assert.Equal(OrderStatus.Accepted, order.Status);

                // 關鍵：API 回報的贏家，跟資料庫裡真正寫進去的，必須是同一個人。
                Assert.Equal(winnerOrder.RiderId, order.RiderId);
            }
            finally
            {
                await SetRidersAvailabilityAsync(riderIds, false);
            }
        }
        [Fact]
        public async Task ClaimOrderAlreadyAccepted_Returns409()
        {
            var client = factory.CreateClient();
            var id = await CreateOrderAsync(client, "測試搶已被搶走的單");//來晚了，看板上已經寫著別人的名字

            await SetRidersAvailabilityAsync(new[] { 3, 4 }, true);

            try
            {
                var firstRes = await client.PostAsJsonAsync($"/api/orders/{id}/claim", new { riderId = 3 });
                firstRes.EnsureSuccessStatusCode();

                var secondRes = await client.PostAsJsonAsync($"/api/orders/{id}/claim", new { riderId = 4 });

                Assert.Equal(HttpStatusCode.Conflict, secondRes.StatusCode);

                var order = await GetOrderFromDbAsync(id);

                Assert.Equal(3, order.RiderId);
            }
            finally
            {
                await SetRidersAvailabilityAsync(new[] { 3, 4 }, false);
            }
        }
        private sealed class CreatedOrder
        {
            public int Id { get; set; }
        }
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };
        private static async Task<int> CreateOrderAsync(HttpClient client, string customerName)
        {
            var res = await client.PostAsJsonAsync("/api/orders", new
            {
                customerName,
                pickupAddress = "台北市中正區重慶南路一段122號",
                dropoffAddress = "台北市信義區市府路1號",
            });

            res.EnsureSuccessStatusCode();

            var created = await res.Content.ReadFromJsonAsync<CreatedOrder>();

            Assert.NotNull(created);

            return created.Id;
        }
        private async Task<Order> GetOrderFromDbAsync(int id)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            return await db.Orders
                .AsNoTracking()
                .SingleAsync(o => o.Id == id);
        }
        private async Task SetRiderAvailabilityAsync(int riderId, bool isAvailable)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            var rider = await db.Riders.SingleAsync(r => r.Id == riderId);
            rider.IsAvailable = isAvailable;

            await db.SaveChangesAsync();
        }
        private async Task SetRidersAvailabilityAsync(int[] riderIds, bool isAvailable)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            await db.Riders
                .Where(r => riderIds.Contains(r.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IsAvailable, isAvailable));
        }
        private static async Task<OrderListResponse> GetOrderListAsync(HttpClient client, string url)
        {
            var res = await client.GetAsync(url);

            res.EnsureSuccessStatusCode();

            var body = await res.Content.ReadFromJsonAsync<OrderListResponse>(JsonOptions);

            Assert.NotNull(body);

            return body;
        }
        private async Task DeleteAllOrdersAsync()
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            await db.Orders.ExecuteDeleteAsync();
        }
        private async Task InsertOrdersDirectlyAsync(int count)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            for (var i = 0; i < count; i++)
            {
                db.Orders.Add(new Order
                {
                    CustomerName = $"批次-{i}",
                    PickupAddress = "台北市中正區重慶南路一段122號",
                    DropoffAddress = "台北市信義區市府路1號",
                    Status = OrderStatus.Created,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            await db.SaveChangesAsync();
        }
    }
}