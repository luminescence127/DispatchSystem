using DispatchSystem.Api.Data;
using DispatchSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace DispatchSystem.Api.Tests
{
    // IClassFixture<DispatchApiFactory>：跟 xUnit 說「這個類別的測試要用那台測試伺服器」。
    // xUnit 會自己建一個 DispatchApiFactory（並呼叫它的 InitializeAsync 起容器），
    // 然後從主建構子的 factory 參數塞給你。
    public class OrdersControllerTests(DispatchApiFactory factory) : IClassFixture<DispatchApiFactory>
    {
        // [Fact] = 「這是一個測試」。沒有它，xUnit 根本不會執行這個方法。
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
        private sealed class CreatedOrder
        {
            public int Id { get; set; }
        }
        private async Task SetRiderAvailabilityAsync(int riderId, bool isAvailable)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            var rider = await db.Riders.SingleAsync(r => r.Id == riderId);
            rider.IsAvailable = isAvailable;

            await db.SaveChangesAsync();
        }
    }
}