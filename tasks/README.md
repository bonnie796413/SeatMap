# Seat Map 實作任務索引

本資料夾彙整「線上座位表系統」的完整實作規劃。每個 `*.md` 為一個獨立模組，
包含：模組目標、前置相依、詳細執行步驟、涉及檔案、API 合約、驗收條件（DoD）。

> 規格來源：`../seat-map.md`（功能規格）、`../seat-map_SA.md`（系統分析）、`../others.md`（部署）。

---

## 架構決策（已確認）

| 項目 | 決策 |
|------|------|
| 後端框架 | ASP.NET Core Web API（Minimal API，net10.0） |
| ORM | Entity Framework Core（Code First） |
| 資料庫 | PostgreSQL + PostGIS（Neon 託管） |
| 空間資料 | NetTopologySuite，座位以 `Point` 幾何儲存 |
| DXF→GeoJSON | `MaxRev.Gdal` NuGet（GDAL .NET 原生繫結），後端 in-process 以 OGR `VectorTranslate` 將 DXF 解析為 GeoJSON（同步處理，不需外部 CLI／`gdal-bin`） |
| GeoJSON 儲存 | Fly.io persistent volume（檔案），後端以靜態路由 `/maps/{floorId}.geojson` 提供 |
| 身分驗證 | ASP.NET Core Identity API Endpoints + Bearer Token（`AddIdentityApiEndpoints` + `MapIdentityApi`；框架內建登入/refresh/密碼管理；前端存 token，帶 `Authorization` header） |
| 前端框架 | Vue 3 + Vite + Pinia + Vue Router |
| 地圖前端 | Leaflet（`CRS.Simple` + `L.geoJSON` 向量圖層） |
| 後端部署 | Fly.io（Docker） |
| 前端部署 | GitHub Pages |
| DB 部署 | Neon |

---

## 模組清單

| # | 檔案 | 模組 | 層 |
|---|------|------|----|
| 00 | [00-foundation-and-infrastructure.md](./00-foundation-and-infrastructure.md) | 基礎建設與專案骨架 | 後端 |
| 01 | [01-database-and-domain-model.md](./01-database-and-domain-model.md) | 資料庫與領域模型 | 後端 |
| 02 | [02-authentication-and-authorization.md](./02-authentication-and-authorization.md) | 身分驗證與授權 | 後端 |
| 03 | [03-floor-management.md](./03-floor-management.md) | 多樓層管理 | 後端 |
| 04 | [04-dxf-tile-pipeline.md](./04-dxf-tile-pipeline.md) | DXF 上傳與 GeoJSON 轉檔 | 後端 |
| 05 | [05-seat-management.md](./05-seat-management.md) | 座位管理 | 後端 |
| 06 | [06-employee-management.md](./06-employee-management.md) | 員工管理 | 後端 |
| 07 | [07-seat-assignment.md](./07-seat-assignment.md) | 座位指派 | 後端 |
| 08 | [08-attendance-checkin.md](./08-attendance-checkin.md) | 打卡與在場狀態 | 後端 |
| 09 | [09-frontend-foundation.md](./09-frontend-foundation.md) | 前端基礎建設 | 前端 |
| 10 | [10-frontend-map-viewer.md](./10-frontend-map-viewer.md) | 地圖瀏覽器 | 前端 |
| 11 | [11-frontend-admin-console.md](./11-frontend-admin-console.md) | 管理者後台 | 前端 |
| 12 | [12-deployment-and-cicd.md](./12-deployment-and-cicd.md) | 部署與 CI/CD | DevOps |
| 13 | [13-testing.md](./13-testing.md) | 測試 | QA |

---

## 相依關係與建議實作順序

```
00 基礎建設
   │
   ▼
01 資料庫與領域模型
   │
   ▼
02 身分驗證 ───────────────────────────────┐
   │                                       │
   ▼                                       │
03 樓層管理 ──► 04 DXF/GeoJSON 轉檔         │（02 為所有需授權的 API 之前置）
   │                                       │
   ▼                                       │
05 座位管理 ──► 06 員工管理 ──► 07 座位指派 ──► 08 打卡
                                              │
   ┌──────────────────────────────────────────┘
   ▼
09 前端基礎 ──► 10 地圖瀏覽器 ──► 11 管理者後台
                                   │
                                   ▼
                               12 部署與 CI/CD
                                   │
                                   ▼
                               13 測試（與各模組並行撰寫，最後總驗收）
```

### 關鍵相依說明
- **01 → 全部後端**：所有後端模組依賴 DbContext 與 Entity。
- **02 → 03~08、11**：所有寫入型 API 與管理者操作需 JWT 授權。
- **04 依賴 03**：GeoJSON 底圖必須掛在已存在的樓層上。
- **05 依賴 03**：座位屬於樓層。
- **07 依賴 05、06**：指派連結座位與員工。
- **08 依賴 06**：打卡對象為員工（登入者）。
- **10 依賴 04、05、08**：地圖需 GeoJSON 底圖、座位、在場狀態 API。
- **12 依賴全部**：所有功能完成後整合部署。

---

## 里程碑

| 里程碑 | 涵蓋模組 | 可驗收成果 |
|--------|----------|------------|
| M1 後端骨架可跑 | 00, 01, 02 | 可登入取得 JWT、DB schema 建立完成 |
| M2 地圖資料就緒 | 03, 04, 05 | 可上傳 DXF 產出 GeoJSON 底圖、建立樓層與座位 |
| M3 業務邏輯完成 | 06, 07, 08 | 員工/指派/打卡 API 全通 |
| M4 前端可視化 | 09, 10 | 地圖顯示底圖、座位、在場狀態、搜尋定位 |
| M5 管理後台 | 11 | 管理者可完成整套地圖設定流程 |
| M6 上線 | 12, 13 | 三端部署完成、測試通過 |

---

## 共用約定

- **API 前綴**：`/api`（例：`/api/auth/login`、`/api/floors`）。
- **身分驗證端點**：`/api/auth/*` 由 `MapIdentityApi<ApplicationUser>()` 自動產生（`/login`、`/register`、`/refresh`、`/manage/info` 等）；額外自訂 `GET /api/auth/me`。
- **回應格式**：成功回傳資源 JSON；錯誤回傳 `ProblemDetails`（RFC 7807）。
- **時間**：一律 UTC（`timestamptz`）。
- **ID**：業務實體主鍵採 `int`（identity）；`ApplicationUser` 主鍵為 `string`（GUID，Identity 內建）；業務層對外暴露 `int` ID。
- **命名**：後端 C# PascalCase；DB snake_case（由 EF 設定轉換，Identity 標準表保持預設命名）；前端 camelCase。
- **角色**：`Employee`、`Admin`（Admin 含所有 Employee 權限；以 Identity `RoleManager` 管理）。
