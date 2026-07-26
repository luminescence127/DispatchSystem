using DispatchSystem.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DispatchSystem.Api.Tests
{
    // ┌─ 這個類別是什麼 ─────────────────────────────────────────────┐
    // │ 一台「測試專用的 API 伺服器」。                               │
    // │ 它會照 Program.cs 把整個 API 跑起來（在記憶體裡，不佔真的埠）， │
    // │ 但連的資料庫換成測試自己起的乾淨容器。                        │
    // └──────────────────────────────────────────────────────────────┘
    //
    // WebApplicationFactory<Program> = 沿用現成的那台測試伺服器，我只改一點
    // IAsyncLifetime               = 跟 xUnit 約定「測試前後各叫我一次」
    public class DispatchApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        // 準備一個 PostgreSQL 17 的容器。
        // 注意：這行只是「把訂單填好」，容器還沒開始跑（下面 StartAsync 才真的跑）。
        private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();

        // 【規則】
        // 伺服器蓋好「之前」，系統會自動呼叫這個方法一次，把還沒蓋好的伺服器交給我們動手腳。
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // 【組態覆蓋順序】
                // 設定是一疊便利貼，「後貼的蓋住先貼的」。
                // Program.cs 原本會從 User Secrets 讀到「我的開發資料庫」，
                // 這裡再貼一張，就變成「測試容器」。原本那張不用撕掉。
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // 冒號代表階層，等同 appsettings.json 裡的
                    // { "ConnectionStrings": { "DispatchDB": "..." } }
                    ["ConnectionStrings:DispatchDB"] = _db.GetConnectionString(),
                });
            });
        }

        // xUnit 在「全部測試開始之前」呼叫這支，一次。
        public async Task InitializeAsync()
        {
            // 第 1 步：容器真的開始跑。跑起來之後才拿得到位址。
            await _db.StartAsync();

            // 第 2 步：新容器裡是一個全空的資料庫，把 migration 套上去，
            //          建出 Orders / Riders 兩張表，順便把 HasData 的兩個 Rider 塞進去。
            //          這三行 = 在命令列打 dotnet ef database update。

            // 順序很重要：碰到 Services 的那一刻，伺服器才真的蓋起來，
            // 也才會去執行上面那行 _db.GetConnectionString()。
            // 所以 StartAsync 一定要排在前面，不然拿到還沒啟動的容器位址。
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
            await db.Database.MigrateAsync();
        }

        // xUnit 在「全部測試跑完之後」呼叫這支，一次：把容器丟掉。
        //
        // 【規則】
        // 方法名前面為什麼要掛「IAsyncLifetime.」：
        // 父類別身上本來就有一個同名方法，直接寫 public 會撞在一起、編譯不過。
        // 掛上介面名字＝「這支是專門給 xUnit 用的」，就不會撞。
        async Task IAsyncLifetime.DisposeAsync()
        {
            await _db.DisposeAsync();
        }
    }
}