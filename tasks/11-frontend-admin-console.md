# 11 — 管理者後台（前端）

## 模組目標

提供管理者完成整套「地圖設定流程」的 UI：樓層管理（新增/改名/排序/刪除）、
DXF 底圖上傳與預覽（含轉檔狀態）、於底圖上點擊新增座位、員工管理（CRUD）、
座位指派。僅 `Admin` 角色可進入。

對應規格：地圖設定流程（1–5）、1.1/1.2 底圖與多樓層、3. 座位指派、員工管理。

## 前置相依

- **09 前端基礎**（auth、路由守衛 `requiresAdmin`、API client）。
- **10 地圖瀏覽器**（重用 Leaflet 地圖元件於座位編輯）。
- 後端 **03/04/05/06/07**。

---

## 後台資訊架構

```
/admin
 ├── /admin/floors      樓層管理 + 底圖上傳 + 座位編輯（地圖編輯模式）
 └── /admin/employees   員工管理 + 座位指派
```

> 管理流程引導：建立樓層 → 上傳 DXF 確認預覽 → 在底圖上點擊新增座位 → 指派員工。

---

## 詳細執行步驟

### 步驟 1：後台外框

新增 `src/views/admin/AdminView.vue`：
- 以 `NLayout` + `NTabs` 建立頂部分頁導覽（分頁：樓層管理、員工管理）。
- 受路由守衛保護（`meta.requiresAdmin`）。

### 步驟 2：樓層管理 UI

新增 `src/views/admin/FloorAdminView.vue`：
- 以 `NDataTable` 顯示樓層清單（依 `displayOrder`），欄位：名稱、座位數、底圖狀態（`NTag` 色彩區分 `Processing` / `Ready` / `Failed`）。
- 拖曳排序：以 `vue-draggable-plus` 包裹 `NDataTable` 的列，拖曳完成後收集新順序並呼叫 `PUT /api/floors/reorder`。
- 操作：
  - 「新增樓層」：`NButton` 觸發 `NModal`，內含 `NForm` + `NInput`（輸入名稱）→ `POST /api/floors`。
  - 「重新命名」：`NButton` 觸發行內 `NInput` 編輯 → `PUT /api/floors/{id}`。
  - 「刪除」：`NButton` 觸發 `useDialog().warning()`（規格：刪除前警告該樓層所有座位與指派將一併移除）→ 確認後 `DELETE`。
- 選取一個樓層 → 進入底圖/座位編輯（步驟 3、4）。

### 步驟 3：DXF 上傳與預覽

新增 `src/components/admin/FloorMapUploader.vue`：
- 以 `NUpload`（`accept=".dxf"`）或 `NUploadDragger` 建立拖放上傳區 → `POST /api/floors/{floorId}/map`（multipart）。
- 上傳後輪詢 `GET /api/floors/{floorId}/map` 顯示狀態：
  - `Processing`：`NSpin` + 提示文字「轉檔中...」。
  - `Failed`：`NAlert`（`type="error"`）顯示 `errorMessage`（規格：解析失敗顯示錯誤並擋上傳）。
  - `Ready`：`NAlert`（`type="success"`）+ 以 Leaflet 載入 Tile 顯示**預覽**，管理者確認方向與比例正確（規格步驟 4）。
- 提供「重新上傳」（`NButton`）覆蓋舊底圖、「移除底圖」（`NButton` `type="error"`）`DELETE`。

### 步驟 4：座位編輯（地圖編輯模式）

新增 `src/components/admin/SeatEditorMap.vue`（重用模組 10 的地圖基礎）：
- 載入該樓層 Tile 底圖與既有座位。
- **新增座位**：點擊底圖任一點 → 取得該點 `latlng`（`CRS.Simple` → `x=lng, y=lat`）→ 以 `NModal`（含 `NForm` + `NInput`）彈出輸入座位編號 → `POST /api/seats`（帶 `floorId,x,y`）。
- **移動座位**：拖曳座位標記（`draggable: true`）→ `dragend` 取得新座標 → `PUT /api/seats/{id}`。
- **編輯/刪除座位**：點擊座位 → 編輯編號（`NInput`）或刪除（`useDialog().warning()` 確認）→ `PUT`/`DELETE`。
  - 刪除確認文字依指派狀態調整：若該座位已有指派（`assignment !== null`），明確標示「此座位已指派給 〇〇〇（`assignment.fullName`），刪除將一併解除其指派並使該員工回到未指派」；未指派則維持一般確認文字。
- **指派員工**：座位浮窗提供「指派員工」以 `NSelect`（`filterable` + `remote` 搜尋員工）→ `POST /api/assignments`；或「解除指派」（`NButton`）`DELETE /api/seats/{seatId}/assignment`。
- 座標換算需與模組 05 約定一致（`x=lng, y=lat`）。

### 步驟 5：員工管理 UI

新增 `src/views/admin/EmployeeAdminView.vue`：
- 以 `NDataTable` 顯示員工清單（`GET /api/employees`）：姓名、部門、帳號、在場狀態（`NTag`）、目前座位。
- 頭像以 `NAvatar` 呈現：有 `avatarUrl` 則顯示圖片，無則以姓名首字作為 fallback。
- 操作：
  - 「新增員工」：`NButton` 觸發 `NModal`（含 `NForm` + `NFormItem` + `NInput`）填寫姓名、部門、頭像 URL、帳號、密碼 → `POST /api/employees`。
  - 「編輯」：同上 Modal 預填欄位，送出 `PUT /api/employees/{id}`。
  - 「刪除」：`useDialog().warning()`（警告將移除帳號、指派、狀態）→ 確認後 `DELETE`。

### 步驟 6：座位指派（於座位編輯地圖）

- 座位指派集中於座位編輯地圖（步驟 4）操作：座位浮窗提供「指派員工」，以 `NSelect`（`filterable` + `remote` 搜尋員工）→ `POST /api/assignments`；或「解除指派」（`NButton`）`DELETE /api/seats/{seatId}/assignment`。
- 指派後即時刷新清單與地圖狀態。

### 步驟 7：表單驗證與回饋

- 必填欄位前端驗證（姓名、座位編號、帳號、密碼）：以 `NForm` + `NFormItem` 的 `rules` prop 實作，驗證錯誤自動顯示於欄位下方。
- 後端錯誤對應顯示：409（座位編號重複/帳號重複/座位已占用）以 `useMessage().error()` 顯示友善訊息。
- 操作成功以 `useMessage().success()` 顯示 toast 提示。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `FrontEnd/src/views/admin/AdminView.vue` | 新增 |
| `FrontEnd/src/views/admin/FloorAdminView.vue` | 新增 |
| `FrontEnd/src/views/admin/EmployeeAdminView.vue` | 新增 |
| `FrontEnd/src/components/admin/FloorMapUploader.vue` | 新增 |
| `FrontEnd/src/components/admin/SeatEditorMap.vue` | 新增 |
| `FrontEnd/src/components/admin/AssignEmployeeDialog.vue` | 新增 |
| `FrontEnd/src/components/admin/EmployeeFormDialog.vue` | 新增 |
| `FrontEnd/src/router/index.ts` | 修改（/admin 子路由） |
| `FrontEnd/src/stores/floors.ts` | 修改（管理操作 actions 重用） |

---

## 驗收條件（DoD）

- [ ] 僅 Admin 可進入 `/admin`，非 Admin 被導回首頁。
- [ ] 可新增、重新命名樓層；拖曳調整順序後刷新仍維持新順序。
- [ ] 刪除樓層前顯示「座位與指派將一併移除」警告，確認後才刪除。
- [ ] 可上傳 `.dxf`，介面顯示轉檔中 → 完成/失敗狀態；失敗顯示錯誤訊息。
- [ ] 底圖 `Ready` 後可預覽，管理者能確認方向與比例。
- [ ] 可在底圖上點擊新增座位並輸入編號；座標正確落在點擊位置。
- [ ] 可拖曳移動座位、編輯編號、刪除座位。
- [ ] 刪除已指派員工的座位時，確認視窗明確標示該員工姓名，確認後 cascade 解除指派。
- [ ] 可將員工指派到座位、解除指派，地圖即時反映。
- [ ] 可新增/編輯/刪除員工；新增後該員工可登入。
- [ ] 後端 409/404 等錯誤在 UI 有友善提示。
- [ ] 整套「樓層→底圖→座位→指派」流程可端到端完成。
