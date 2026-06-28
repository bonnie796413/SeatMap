# 13 — 測試

## 模組目標

為後端與前端建立測試，確保各模組功能正確並可回歸。涵蓋後端單元/整合測試、
前端單元/元件測試，以及一份端到端（E2E）手動驗收清單。測試隨各功能模組並行撰寫，
最後做整體驗收。

> 規格未要求測試，本模組為工程品質保險，可依時程調整深度（建議至少完成「最小可信賴測試集」）。

## 前置相依

- 對應被測模組（撰寫該模組測試前需其實作就緒，或採 TDD 同步進行）。

---

## 測試策略總覽

| 層級 | 範圍 | 工具 |
|------|------|------|
| 後端單元 | Service 業務邏輯（指派策略、打卡狀態、座標轉換） | xUnit |
| 後端整合 | Endpoint + DB（含 PostGIS） | xUnit + `WebApplicationFactory` + Testcontainers（postgis）|
| 前端單元 | store / api 封裝 / 工具函式 | Vitest |
| 前端元件 | 關鍵元件（登入、座位標記、搜尋） | Vitest + Vue Test Utils |
| E2E / 手動 | 端到端使用流程 | 手動清單 |

---

## A. 後端測試

### 步驟 A1：建立測試專案
- 新增 `BackEnd.Tests/`（xUnit），參考 `BackEnd`。
- 套件：`xunit`、`xunit.runner.visualstudio`、`Microsoft.AspNetCore.Mvc.Testing`、`Testcontainers.PostgreSql`、`FluentAssertions`。

### 步驟 A2：整合測試基礎建設
- 以 `WebApplicationFactory<Program>` 啟動 API（需 `Program` 可被測試存取：加 `public partial class Program {}`）。
- 以 **Testcontainers** 啟動 `postgis/postgis` 容器，覆寫連線字串，套用 Migration。
- 提供測試輔助：以 `UserManager` 建立管理者/員工帳號並指派角色、呼叫 `POST /api/auth/login` 取得 `accessToken` 的 helper。

### 步驟 A3：單元測試（Service 層）
重點涵蓋具邏輯分支者：
- `AssignmentService`：
  - 指派空座位成功。
  - 座位已被他人占用 → 衝突。
  - 員工改派（舊指派被移除、僅一座位）。
  - 重複指派同座位冪等。
- `AttendanceService`：
  - 上班打卡 → `IsPresent=true` + 時間戳。
  - 下班打卡 → `IsPresent=false`。
  - 冪等重複打卡。
- `SeatService` 座標轉換：`x,y` ↔ `Point(X,Y)` 來回一致。
- `AdditionalUserClaimsPrincipalFactory`：有員工帳號登入後 `HttpContext.User` 含 `employeeId` claim；無員工管理者登入則不含。

### 步驟 A4：整合測試（Endpoint + DB）
- **Auth**：`POST /api/auth/login` 成功取得 `accessToken`+`refreshToken`；錯誤密碼非 200；`/me` 還原 `employeeId`；`/refresh` 換新 token。
- **授權**：未帶 token 401；非 Admin 打 AdminOnly 403。
- **Floor**：CRUD、reorder 後順序正確、刪除 cascade（座位/指派一併消失）。
- **Seat**：同樓層編號重複 409；建立/更新/刪除；`listByFloor` 含指派與在場狀態。
- **Employee**：新增同時建帳號（可登入）；username 重複 409 且交易回滾；search 模糊比對。
- **Assignment**：一對一約束（DB 唯一鍵）、改派、解除。
- **Attendance**：打卡後 `listByFloor` 的 `isPresent` 反映；無 employeeId 的管理者打卡 400。
- **健康檢查**：`/health` 200。

### 步驟 A5：DXF→GeoJSON 轉檔測試
- 上傳合法 `.dxf` → 同步轉檔 → `Status=Ready`，產出 `/maps/{floorId}.geojson`（以小型測試 DXF 實跑 `MaxRev.Gdal`，斷言 GeoJSON 含預期 feature）。
- 非 `.dxf` 上傳或解析失敗被拒（400），`Status=Failed` 且 `errorMessage` 有內容。
- 樓層刪除呼叫 `IFloorMapStorage.DeleteFloorMapAsync`（以 mock 驗證）。

---

## B. 前端測試

### 步驟 B1：安裝測試工具
於 `FrontEnd/`：
```pwsh
npm install -D vitest @vue/test-utils jsdom
```
- `package.json` 加 `"test:unit": "vitest"`。

### 步驟 B2：單元測試
- `stores/auth`：login 後狀態/`isAdmin`/`isAuthenticated`、logout 清除、`accessToken`+`refreshToken` localStorage 持久化、`refresh()` action 成功換 token 及失敗後 logout。
- `api/http`：請求攔截器注入 `Authorization`；401 先嘗試 refresh，失敗才觸發登出（以 mock axios）。
- 座標換算工具：`x,y` ↔ Leaflet `latlng` 一致。

### 步驟 B3：元件測試
- `LoginView`：輸入帳密送出、錯誤訊息顯示。
- 座位標記產生（`seatMarkers`）：在場/不在場/空座位的 icon class 正確。
- `EmployeeSearch`：輸入觸發查詢（mock API）、選取結果發出定位事件。

> Leaflet 依賴 DOM/canvas，元件測試以 jsdom 為主，地圖實體互動可 mock 或留待 E2E。

---

## C. 端到端驗收清單（手動）

對照規格「狀態流程」與「地圖設定流程」，逐項人工驗收：

1. 管理者登入 → 進入 `/admin`。
2. 新增樓層「3F 研發部」。
3. 上傳 `.dxf` → 等待轉檔 Ready → 預覽底圖。
4. 在底圖點擊新增數個座位並編號。
5. 新增員工（含帳號）→ 指派到座位。
6. 以該員工帳號登入 → 地圖定位其座位。
7. 員工「上班打卡」→ 座位變綠。
8. 另一視窗/輪詢確認在場狀態反映。
9. 員工「下班打卡」→ 座位變灰。
10. 搜尋員工姓名 → 自動定位座位。
11. 多樓層輪播切換、重置視角、縮放平移、觸控操作。
12. 刪除樓層 → 警告 → 座位與指派一併移除。

---

## D. CI 整合（與模組 12 串接）

- 後端 workflow 在 deploy 前執行 `dotnet test`（Testcontainers 需 CI runner 支援 Docker；GitHub Actions ubuntu runner 可用）。
- 前端 workflow 在 build 前執行 `npm run test:unit -- --run`。
- 測試失敗則中止部署。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd.Tests/BackEnd.Tests.csproj` | 新增 |
| `BackEnd.Tests/Integration/*.cs` | 新增 |
| `BackEnd.Tests/Unit/*.cs` | 新增 |
| `BackEnd.Tests/TestHelpers/*.cs`（Factory、Auth helper） | 新增 |
| `BackEnd/Program.cs` | 修改（`public partial class Program`） |
| `FrontEnd/package.json` | 加 vitest 等 + test script |
| `FrontEnd/src/**/__tests__/*.spec.ts` | 新增 |
| `FrontEnd/vitest.config.ts`（或併入 vite config） | 新增 |
| `.github/workflows/*.yml` | 修改（加測試步驟） |

---

## 驗收條件（DoD）

- [ ] 後端測試專案可執行 `dotnet test` 全綠。
- [ ] 整合測試以 Testcontainers 啟動 PostGIS，涵蓋 Auth/Floor/Seat/Employee/Assignment/Attendance 主要路徑。
- [ ] 後端單元測試涵蓋指派策略、打卡狀態、座標轉換、`AdditionalUserClaimsPrincipalFactory` claims 注入。
- [ ] 前端 `npm run test:unit` 全綠，涵蓋 auth store（含 refresh 流程）、http 攔截、座標換算與關鍵元件。
- [ ] 端到端手動驗收清單 12 項全數通過。
- [ ] CI 在部署前執行測試，失敗即阻擋部署。
- [ ] 關鍵業務規則（一員工一座位、座位編號同樓層唯一、打卡即時反映）均有對應測試保護。
