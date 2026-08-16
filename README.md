# DispatchSystem

使用 ASP.NET Core 9 開發的訂單指派 Web API。

## 技術

- ASP.NET Core 9
- Entity Framework Core 9
- PostgreSQL 17
- xUnit
- WebApplicationFactory
- Testcontainers
- Docker Compose
- GitHub Actions

## API

| Method | Endpoint | 功能 |
|---|---|---|
| `POST` | `/api/orders` | 建立訂單 |
| `GET` | `/api/orders/{id}` | 查詢單筆訂單 |
| `GET` | `/api/orders` | 查詢訂單清單 |
| `POST` | `/api/orders/{id}/assign` | 指派訂單 |
| `POST` | `/api/orders/{id}/claim` | 搶單 |
| `POST` | `/api/orders/{id}/accept` | 接受訂單 |
| `POST` | `/api/orders/{id}/complete` | 完成訂單 |

清單查詢支援：

- 依訂單狀態篩選
- 分頁
- 依 Id 由大到小排序
- 每頁最多 100 筆
- 回傳總筆數

## 訂單狀態流轉

一般指派流程：

`Created → Assigned → Accepted → Completed`

搶單流程：

`Created → Accepted → Completed`

狀態不符合操作條件時回傳 `409 Conflict`。

## HTTP 回應

- 建立訂單成功：`201 Created`
- 輸入驗證失敗：`400 Bad Request`
- 找不到訂單或 Rider：`404 Not Found`
- 訂單狀態、Rider 狀態或資料版本衝突：`409 Conflict`

狀態衝突透過 `Problem()` 回傳 ProblemDetails，內容包含 `status`、`title` 與 `detail`。

## 併發控制

`Order` 使用 PostgreSQL `xmin` 作為 EF Core concurrency token。

claim API 儲存資料時若發生 `DbUpdateConcurrencyException`，會回傳 `409 Conflict`。

相關測試包含：

- 使用兩個 DbContext 讀取同一筆 Order，確認舊版本資料無法再次寫入。
- 8 個 Rider 平行呼叫同一筆 Order 的 claim API，確認一個 request 回傳成功，其餘七個回傳 `409 Conflict`。
- 回查資料庫，確認成功 response 的 RiderId 與實際寫入資料一致。

## Integration Test

測試使用 WebApplicationFactory 啟動 ASP.NET Core application，並透過 HttpClient 呼叫 API。

DispatchApiFactory 會：

1. 使用 Testcontainers 啟動 PostgreSQL 17。
2. 將測試環境的 connection string 指向測試 container。
3. 套用專案中的 EF Core Migration。
4. 在測試完成後移除 container。

OrdersControllerTests 共用同一個 DispatchApiFactory。每個測試開始前會清除 Orders，再建立測試需要的資料。

測試涵蓋：

- 訂單建立與資料庫寫入
- 單筆查詢與不存在資料
- 狀態篩選
- 排序與分頁
- pageSize 上限
- 訂單指派、接受與完成
- 非法狀態流轉
- Rider 是否可接單
- 搶單結果
- optimistic concurrency conflict
- 平行 request 搶單

執行測試：

```bash
dotnet test
```

執行測試前需啟動 Docker。

## 本機執行

需要：

- .NET 9 SDK
- Docker
- EF Core CLI 9.x

設定 PostgreSQL 密碼並啟動資料庫：

```powershell
$env:POSTGRES_PASSWORD="your_password"
docker compose up -d
```

設定 connection string：

```powershell
dotnet user-secrets set `
  "ConnectionStrings:DispatchDB" `
  "Host=localhost;Port=5433;Database=dispatchsystem;Username=postgres;Password=your_password" `
  --project src/DispatchSystem.Api
```

套用 Migration：

```powershell
dotnet ef database update --project src/DispatchSystem.Api
```

啟動 API：

```powershell
dotnet run --project src/DispatchSystem.Api
```

開發環境網址：

- API Reference：`http://localhost:5106/scalar/v1`
- OpenAPI document：`http://localhost:5106/openapi/v1.json`
- Health Check：`http://localhost:5106/health`
- Application information：`http://localhost:5106/about`

## CI

GitHub Actions 在 push 與 pull request 時執行：

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Workflow 使用 Ubuntu runner 與 .NET 9。