# 05 — 座位管理

## 模組目標

實作管理者於底圖上新增、編輯、刪除座位標記，每個座位以 PostGIS `Point` 儲存座標
（Leaflet `CRS.Simple` 像素座標系），含座位編號。提供前端載入某樓層全部座位（含在場狀態與指派員工）的查詢。

對應規格：1.4 座位標記、地圖設定流程步驟 3；改寫既有 `SeatEndpoints.cs` stub。

> 分層說明：本模組僅負責座標（`Point`）與指派／在場狀態「資料」供前端渲染，**刻意不含前端渲染**。
> 座位於底圖上的標示樣式（已指派＝頭像／`PersonFilled`、未指派空座位＝`EventSeatFilled`）屬前端範疇，詳見模組 10 步驟 5。

## 前置相依

- **01 資料庫**（`Seat`、NetTopologySuite `Point`）。
- **02 身分驗證**（新增/編輯/刪除為管理者）。
- **03 樓層管理**（座位屬於樓層）。
- 軟相依 **04**（座標系與 bounds 對齊 Tile）、**06/07/08**（座位查詢回傳指派員工與在場狀態）。

---

## 業務規則

- 座位編號 `SeatNumber` 在「同一樓層內」唯一（DB 已建複合唯一索引）。
- 座標 `Location` 為底圖像素座標（與模組 04 的 bounds 一致；Leaflet `CRS.Simple` 下 `latlng = [y, x]`）。
  - **約定**：後端以 `Point(X=像素x, Y=像素y)` 儲存；前端轉換為 Leaflet `L.latLng(y, x)`。
- 刪除座位 → cascade 移除其 `SeatAssignment`（員工變未指派）。
- 既有 `BackEnd/Endpoints/SeatEndpoints.cs` 的 5 個 TODO stub 全數以真實邏輯取代，並改為 `/api` 群組與授權。

---

## 詳細執行步驟

### 步驟 1：DTO 定義

於 `BackEnd/Dtos/Seats/`：
- `SeatResponse`：`id`、`floorId`、`seatNumber`、`x`、`y`、`assignment`（null 或 `{employeeId,fullName,avatarUrl,department}`）、`isPresent`（無指派為 null）。
- `CreateSeatRequest`：`floorId`、`seatNumber`、`x`、`y`。
- `UpdateSeatRequest`：`seatNumber`、`x`、`y`（允許搬移座位位置與改編號）。

### 步驟 2：座標轉換工具

- 後端讀寫使用 `NetTopologySuite.Geometries.Point`：
  - 寫入：`new Point(x, y) { SRID = 0 }`（CRS.Simple 無地理投影）。
  - 讀出：`seat.Location.X`、`seat.Location.Y` → DTO 的 `x`、`y`。
- 集中於 `SeatService` 內，避免散落。

### 步驟 3：Seat 服務

新增 `BackEnd/Services/SeatService.cs`：
- `GetByFloorAsync(floorId)`：回該樓層所有座位，`Include` 指派與員工、`AttendanceState`，組為 `SeatResponse[]`。
  - 這是地圖顯示的主要資料來源（模組 10）。
- `GetByIdAsync(id)`。
- `CreateAsync(req)`：驗證樓層存在、同樓層編號未重複 → 建立。
- `UpdateAsync(id, req)`：可改編號（檢查同樓層唯一）與座標。
- `DeleteAsync(id)`：刪除（cascade 指派）。

### 步驟 4：改寫 SeatEndpoints

重寫 `BackEnd/Endpoints/SeatEndpoints.cs`，掛 `/api`：

- `GET /api/floors/{floorId}/seats`（需登入）：列出該樓層座位（地圖載入用）。
- `GET /api/seats/{id}`（需登入）：單一座位（點擊浮窗用）。
- `POST /api/seats`（AdminOnly）：新增座位。
- `PUT /api/seats/{id}`（AdminOnly）：更新編號/座標。
- `DELETE /api/seats/{id}`（AdminOnly）：刪除。

> 移除舊有 `MapGet("/seats")` 等未授權 stub 路由，統一改為上述。

### 步驟 5：驗證與錯誤

- `seatNumber` 空白 → 400。
- 同樓層重複編號 → 409。
- 樓層不存在 → 404。
- `x/y` 超出底圖 bounds（若 `FloorMap.Ready`）→ 回傳 400 並附帶警告訊息，不寫入 DB。

### 步驟 6：效能

- `GetByFloorAsync` 一次查詢含必要 join，避免 N+1（用 `Include`/投影）。
- 座位數量級不大（單樓層數十～數百），不需空間索引查詢；但 `Location` 已是 geometry，未來可擴充。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/Dtos/Seats/*.cs` | 新增 |
| `BackEnd/Services/SeatService.cs` | 新增 |
| `BackEnd/Endpoints/SeatEndpoints.cs` | 改寫（取代 stub） |
| `BackEnd/Program.cs` | 修改（DI、endpoint 掛載） |

---

## API 合約（本模組）

| Method | Path | 授權 | 請求 | 回應 |
|--------|------|------|------|------|
| GET | `/api/floors/{floorId}/seats` | 需登入 | — | `SeatResponse[]` |
| GET | `/api/seats/{id}` | 需登入 | — | `SeatResponse` |
| POST | `/api/seats` | Admin | `{floorId,seatNumber,x,y}` | `SeatResponse`（201） |
| PUT | `/api/seats/{id}` | Admin | `{seatNumber,x,y}` | `SeatResponse` |
| DELETE | `/api/seats/{id}` | Admin | — | 204 |

`SeatResponse` 範例：
```json
{
  "id": 12, "floorId": 3, "seatNumber": "A-12",
  "x": 540.5, "y": 312.0,
  "assignment": { "employeeId": 7, "fullName": "王小明", "avatarUrl": null, "department": "研發" },
  "isPresent": true
}
```

---

## 驗收條件（DoD）

- [ ] 管理者可在指定樓層新增座位，座標以 `Point` 寫入 DB 並可讀回相同 `x/y`。
- [ ] 同樓層重複 `seatNumber` 回 409。
- [ ] 可更新座位編號與座標（搬移位置）。
- [ ] 刪除座位後其指派一併移除（員工回到未指派）。
- [ ] `GET /api/floors/{floorId}/seats` 回傳含指派員工與在場狀態（與模組 07/08 串接後）。
- [ ] 未指派座位的 `assignment` 為 null、`isPresent` 為 null（前端顯示為空座位）。
- [ ] 既有 SeatEndpoints stub 已全部以真實邏輯取代並套用授權。
- [ ] 非管理者無法新增/編輯/刪除座位（403）。
