# 00 — 基礎建設與專案骨架

## 模組目標

建立後端解決方案的基礎結構，提供後續所有模組共用的設定、跨域、錯誤處理、
健康檢查與設定管理。將既有 scaffold（WeatherEndpoints、SeatEndpoints stub）整理為
正式可擴充的專案骨架。

## 前置相依

- 無（最先執行）。
- 現況：`BackEnd/` 已是 net10.0 Minimal API 專案，含 `Program.cs`、`Endpoints/SeatEndpoints.cs`（TODO stub）、`Endpoints/WeatherEndpoints.cs`（範例）。

## 現況檢查

| 既有檔案 | 處置 |
|----------|------|
| `BackEnd/Program.cs` | 改寫為正式 pipeline |
| `BackEnd/Endpoints/WeatherEndpoints.cs` | 刪除（範例） |
| `BackEnd/Endpoints/SeatEndpoints.cs` | 保留結構，內容於模組 05 重寫 |
| `BackEnd/appsettings.json` | 擴充設定區段 |

---

## 詳細執行步驟

### 步驟 1：規劃資料夾結構

於 `BackEnd/` 下建立以下資料夾（隨模組逐步填入）：

```
BackEnd/
├── Endpoints/        # Minimal API endpoint 群組（每個資源一檔）
├── Data/             # DbContext、設定、Seeder（模組 01）
├── Entities/         # 領域實體（模組 01）
├── Dtos/             # 請求/回應 DTO
├── Services/         # 業務服務（Auth、FloorMap、Attendance…）
├── Options/          # 強型別設定類別（MapStorageOptions、GeoJsonConversionOptions…）
├── Infrastructure/   # 共用：例外處理、擴充方法
└── Migrations/       # EF Core migration（自動產生）
```

> 空資料夾以放置首個檔案的方式建立；不需 placeholder 檔。

### 步驟 2：移除範例程式碼

1. 刪除 `BackEnd/Endpoints/WeatherEndpoints.cs`。
2. 刪除 `BackEnd/BackEnd.http` 內 weatherforecast 範例（之後改放實際 API 範例）。
3. `Program.cs` 移除 `app.MapWeatherEndpoints();`。

### 步驟 3：加入基礎 NuGet 套件

於 `BackEnd/BackEnd.csproj` 加入（版本以還原當下最新穩定為準）：

- `Microsoft.AspNetCore.OpenApi`（已有）
- `Scalar.AspNetCore`（已有，作為 API 文件 UI）

> EF Core / Npgsql / JWT 套件於對應模組（01、02）再加入，避免一次塞太多。

指令：
```pwsh
dotnet restore
```

### 步驟 4：建立強型別設定基礎（Options 模式）

1. 新增 `BackEnd/Options/CorsOptions.cs`：包含 `AllowedOrigins: string[]`。
2. 於 `appsettings.json` 加入：
   ```json
   "Cors": { "AllowedOrigins": [ "http://localhost:8000" ] }
   ```
   - `appsettings.Development.json` 保留本機前端來源。
   - 正式環境（Fly.io）以環境變數覆寫為 GitHub Pages 網域（模組 12）。

### 步驟 5：設定 CORS

於 `Program.cs`：
1. 讀取 `Cors:AllowedOrigins`。
2. `AddCors` 註冊具名 policy `"Frontend"`：允許設定來源、所有標頭、`GET/POST/PUT/DELETE`、允許 `Authorization` 標頭。
3. `app.UseCors("Frontend")` 置於驗證中介軟體之前。

### 步驟 6：全域例外處理與 ProblemDetails

1. `Program.cs` 加入 `builder.Services.AddProblemDetails()`。
2. 新增 `BackEnd/Infrastructure/GlobalExceptionHandler.cs`，實作 `IExceptionHandler`：
   - 將未處理例外轉為 `ProblemDetails`（500），記錄 log，不洩漏堆疊。
   - 自訂例外（如 `NotFoundException`、`ValidationException`）對應 404 / 400。
3. 註冊 `builder.Services.AddExceptionHandler<GlobalExceptionHandler>();` 與 `app.UseExceptionHandler();`。

### 步驟 7：健康檢查

1. `builder.Services.AddHealthChecks();`（模組 01 後追加 DB 檢查）。
2. `app.MapHealthChecks("/health");`（不需授權）。

### 步驟 8：API 路由前綴與分組約定

- 所有業務 endpoint 掛在 `/api` 之下。
- 於 `Program.cs` 建立 `var api = app.MapGroup("/api");`，各模組以 `api.MapXxxEndpoints()` 擴充。
- Endpoint 擴充方法簽章統一為 `public static RouteGroupBuilder MapXxxEndpoints(this IEndpointRouteBuilder app)`。

### 步驟 9：開發文件 UI

- 保留 `MapOpenApi()` 與 `MapScalarApiReference()`，僅在 Development 啟用。
- 確認啟動後 `/scalar/v1` 可瀏覽。

### 步驟 10：HTTPS / 連接埠

- 本機沿用 `launchSettings.json`（http 7176 / https 7176、5156）。
- 作法：僅在 Development 呼叫 `app.UseHttpsRedirection()`。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/Program.cs` | 改寫 |
| `BackEnd/Endpoints/WeatherEndpoints.cs` | 刪除 |
| `BackEnd/Options/CorsOptions.cs` | 新增 |
| `BackEnd/Infrastructure/GlobalExceptionHandler.cs` | 新增 |
| `BackEnd/Infrastructure/AppExceptions.cs`（NotFound/Validation/Conflict） | 新增 |
| `BackEnd/appsettings.json` | 修改 |
| `BackEnd/appsettings.Development.json` | 修改 |
| `BackEnd/BackEnd.http` | 整理 |

---

## API 合約（本模組）

| Method | Path | 授權 | 說明 |
|--------|------|------|------|
| GET | `/health` | 否 | 健康檢查，回傳 200 + 狀態 |

---

## 驗收條件（DoD）

- [ ] `dotnet build` 成功，無警告（nullable 已啟用）。
- [ ] `dotnet run` 後 `/health` 回 200。
- [ ] Development 下 `/scalar/v1` 可開啟。
- [ ] 範例 Weather endpoint 已移除，`/weatherforecast` 回 404。
- [ ] CORS policy `"Frontend"` 生效：以 `http://localhost:8000` 來源的預檢請求（OPTIONS）回應正確標頭。
- [ ] 故意丟出未處理例外時，回應為 `application/problem+json` 且狀態 500，不含堆疊細節。
- [ ] 所有業務 endpoint 預留掛載於 `/api` 群組。
