# 03 — 多樓層管理

## 模組目標

實作樓層的新增、重新命名、刪除與排序（拖曳順序）。樓層為獨立的底圖與座位集合。
刪除樓層時連帶移除其下所有座位與指派關係，並於 API 層提供必要的關聯資訊供前端警告。

對應規格：1.2 多樓層管理、地圖設定流程步驟 1。

## 前置相依

- **01 資料庫**（`Floor`、`FloorMap`、`Seat` 實體與 cascade 設定）。
- **02 身分驗證**（樓層 CRUD 為管理者操作）。

---

## 業務規則

- 樓層名稱必填，可重複（不強制唯一，但建議前端提示）。
- `DisplayOrder` 決定地圖輪播順序（模組 10）；新增時預設排在最後。
- 刪除樓層 → cascade 刪除 `Seat`、`SeatAssignment`、`FloorMap`，並需清理底圖檔案（GeoJSON 與原始 DXF，呼叫模組 04 的清理邏輯）。
- 排序更新採批次：前端傳入有序的 floorId 陣列，後端重寫 `DisplayOrder`。

---

## 詳細執行步驟

### 步驟 1：DTO 定義

於 `BackEnd/Dtos/Floors/`：
- `FloorResponse`：`id`、`name`、`displayOrder`、`seatCount`、`mapStatus`（取自 FloorMap.Status，無底圖為 `None`）。
- `CreateFloorRequest`：`name`。
- `UpdateFloorRequest`：`name`。
- `ReorderFloorsRequest`：`orderedFloorIds: int[]`。

### 步驟 2：Floor 服務

新增 `BackEnd/Services/FloorService.cs`：
- `GetAllAsync()`：依 `DisplayOrder` 排序，含 `seatCount`（`Count` 子查詢）與 `mapStatus`。
- `GetByIdAsync(id)`。
- `CreateAsync(name)`：`DisplayOrder` = 目前最大值 + 1。
- `RenameAsync(id, name)`。
- `ReorderAsync(orderedFloorIds)`：驗證 id 集合與 DB 一致 → 依陣列索引寫回 `DisplayOrder`（單一交易）。
- `DeleteAsync(id)`：
  1. 載入樓層與其 `FloorMap`。
  2. 刪除 DB（cascade 處理座位/指派）。
  3. 呼叫 `IFloorMapStorage.DeleteFloorMapAsync(floorId)`（模組 04 提供；本模組先以介面注入，未完成前可空實作）。

### 步驟 3：Endpoints

新增 `BackEnd/Endpoints/FloorEndpoints.cs`，掛 `/api/floors`：

- `GET /api/floors`（需登入）：列出所有樓層（員工也需用於切換樓層）。
- `GET /api/floors/{id}`（需登入）。
- `POST /api/floors`（AdminOnly）：建立。
- `PUT /api/floors/{id}`（AdminOnly）：重新命名。
- `PUT /api/floors/reorder`（AdminOnly）：批次排序。
- `DELETE /api/floors/{id}`（AdminOnly）：刪除（連帶清理）。

> 刪除前的「警告」由前端負責顯示（模組 11）；後端可在 `GET /api/floors/{id}` 或刪除回應附帶受影響的 `seatCount`，讓前端據以提示。

### 步驟 4：驗證與錯誤處理

- `name` 空白 → 400。
- 找不到樓層 → 404。
- `reorder` 的 id 集合與現有不符 → 400。

### 步驟 5：交易與一致性

- `ReorderAsync`、`DeleteAsync` 以單一 `SaveChanges`/交易完成，避免部分更新。
- 底圖檔案清理失敗不應回滾 DB 刪除，但需記 log 並回報（避免孤兒檔案；可由背景清理補償）。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/Dtos/Floors/*.cs` | 新增 |
| `BackEnd/Services/FloorService.cs` | 新增 |
| `BackEnd/Services/IFloorMapStorage.cs`（介面，實作於模組 04） | 新增 |
| `BackEnd/Endpoints/FloorEndpoints.cs` | 新增 |
| `BackEnd/Program.cs` | 修改（DI 註冊、endpoint 掛載） |

---

## API 合約（本模組）

| Method | Path | 授權 | 請求 | 回應 |
|--------|------|------|------|------|
| GET | `/api/floors` | 需登入 | — | `FloorResponse[]`（依序） |
| GET | `/api/floors/{id}` | 需登入 | — | `FloorResponse` |
| POST | `/api/floors` | Admin | `{name}` | `FloorResponse`（201） |
| PUT | `/api/floors/{id}` | Admin | `{name}` | `FloorResponse` |
| PUT | `/api/floors/reorder` | Admin | `{orderedFloorIds:[]}` | `FloorResponse[]` |
| DELETE | `/api/floors/{id}` | Admin | — | 204 |

---

## 驗收條件（DoD）

- [ ] 管理者可建立樓層，`displayOrder` 自動排在最後。
- [ ] 可重新命名樓層。
- [ ] `reorder` 後 `GET /api/floors` 回傳順序與送入陣列一致。
- [ ] 刪除樓層後，該樓層的座位與指派在 DB 中一併消失（cascade 驗證）。
- [ ] 刪除樓層會觸發底圖清理介面呼叫（`IFloorMapStorage`，以 log 或測試替身驗證）。
- [ ] 非管理者呼叫 `POST/PUT/DELETE` 回 403；未登入回 401。
- [ ] `GET /api/floors` 對一般員工可用（供樓層切換）。
- [ ] `FloorResponse.seatCount` 與 `mapStatus` 正確反映現況。
