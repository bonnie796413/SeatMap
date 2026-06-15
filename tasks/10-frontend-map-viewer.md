# 10 — 地圖瀏覽器（前端）

## 模組目標

以 Leaflet（`CRS.Simple`）建立座位地圖瀏覽器：載入樓層 Tile 底圖、繪製座位標記
（頭像/姓名首字、在場狀態色彩、空座位樣式）、縮放/平移/觸控、重置視角、
多樓層輪播切換、點擊座位浮窗、員工搜尋定位、員工本人打卡，並以輪詢即時更新在場狀態。

對應規格：1.3 地圖瀏覽、1.4 座位標記、2. 打卡（前端觸發）、4. 員工查詢。

## 前置相依

- **09 前端基礎**（API client、auth、路由、Leaflet 安裝）。
- 後端 **03/04/05/06/08**（樓層、Tile、座位、員工搜尋、打卡）。

---

## Leaflet CRS.Simple 要點（實作依據）

- 建立地圖：`L.map(el, { crs: L.CRS.Simple, minZoom, maxZoom })`。
- 底圖 TileLayer：`L.tileLayer('/tiles/{floorId}/{z}/{x}/{y}.png', { minZoom, maxZoom, tileSize: 256, noWrap: true, bounds })`。
  - `tileUrlTemplate` 由後端 `GET /api/floors/{floorId}/map` 提供（已含 floorId）。
  - 完整 URL：`VITE_API_BASE_URL` 對應後端 + `/tiles/...`（注意：tiles 不在 `/api` 之下，需用後端站台根，見步驟 2）。
- 座標系：`CRS.Simple` 下座標為 `[y, x]`（`L.latLng(y, x)`）。後端 Seat 存 `x,y` 像素 → 前端用 `L.latLng(seat.y, seat.x)`。
- 視野範圍：以 `FloorMap.bounds`（`[[0,0],[height,width]]`）設 `map.setMaxBounds` 並 `fitBounds`。
- 搜尋定位：`map.setView([y, x], zoom)`。

---

## 詳細執行步驟

### 步驟 1：地圖容器與初始化

新增 `src/components/map/SeatMap.vue`：
- 掛載一個全尺寸 `<div ref="mapEl">`。
- `onMounted` 初始化 Leaflet map（`CRS.Simple`），`onBeforeUnmount` 銷毀。
- 互動預設即支援：滾輪縮放、左鍵拖曳平移。
- 觸控：Leaflet 內建支援雙指縮放與單指拖曳（確認 `touchZoom`、`dragging` 啟用）。

### 步驟 2：Tile 圖層 URL 處理

- Tile 路徑非 `/api` 前綴，需後端「站台根」URL。
- 於 `src/api/http.ts` 或設定中額外提供 `API_ORIGIN`（由 `VITE_API_BASE_URL` 去除 `/api` 推導），組成 `${API_ORIGIN}/tiles/{floorId}/{z}/{x}/{y}.png`。
- 載入新樓層時移除舊 TileLayer、加入新 TileLayer，並依該樓層 `minZoom/maxZoom/bounds` 重設。

### 步驟 3：樓層狀態管理（Pinia）

新增 `src/stores/floors.ts`：
- state：`floors`（依序）、`currentIndex`、`currentFloor`、`currentMapMeta`（FloorMap）。
- actions：
  - `loadFloors()`：`GET /api/floors`。
  - `selectFloor(index)`：設定當前樓層 → 載入其 map meta（`GET /api/floors/{id}/map`）與座位。
  - `next()` / `prev()`：輪播切換（循環或邊界停止；規格為「下一樓層」箭頭，採循環）。

### 步驟 4：樓層輪播 UI

於 `SeatMap.vue` 疊加控制層：
- **左上角**：以 `NTag` 固定顯示目前樓層名稱（`currentFloor.name`）。
- **右側**：以 `NButton`（搭配 `@vicons/material` 的箭頭圖示）點擊 `floorsStore.next()` 切換至下一樓層。
- 切換時：替換 Tile 底圖、清除並重繪座位、`fitBounds` 到新樓層。

### 步驟 5：座位標記繪製

新增 `src/components/map/seatMarkers.ts`（或於 SeatMap 內）：
- 載入該樓層座位：`GET /api/floors/{floorId}/seats`。
- 每個座位以 `L.marker([seat.y, seat.x], { icon })`，icon 用 `L.divIcon`：
  - **有指派**：圓形/方形標記，內含頭像 `<img>`（`avatarUrl`）或姓名首字 / `PersonFilled` 圖示（無頭像時的 fallback）。
  - **在場狀態色**：`isPresent === true` → 綠色邊框/底；`false` → 灰色。
  - **未指派座位**（`assignment === null`）：以 `@vicons/material` 的 **`EventSeatFilled`** 座椅圖示（SVG 字串嵌入 `L.divIcon`），搭配 `.seat-empty` 淺色虛線外框樣式。
- 圖示 SVG 取得方式比照 SA 既有 `PersonFilled` 用法（`@vicons/material` 匯入後以 `renderToString` 或元件取出 SVG 字串），與其他圖示一致。
- 以 `L.layerGroup` 管理所有座位標記，切換樓層時整組清除重建。
- icon 樣式以 CSS class 控制（`.seat-marker`, `.seat-present`, `.seat-absent`, `.seat-empty`）。

### 步驟 6：座位點擊浮窗

- 標記綁定 `bindPopup` 或自訂浮窗元件 `SeatPopup.vue`：
  - 顯示：姓名、部門（規格 1.4「員工基本資訊浮窗（姓名、部門）」）。
  - 空座位浮窗：顯示「未指派」+ 座位編號。
- Hover 顯示姓名（tooltip `bindTooltip`），點擊顯示完整資訊（popup）。

### 步驟 7：重置視角

- 控制列加「重置視角」按鈕 → `map.fitBounds(currentBounds)`（回到預設縮放與置中）。

### 步驟 8：員工搜尋定位

新增 `src/components/map/EmployeeSearch.vue`：
- 以 `NSelect`（`filterable` + `remote`）實作：輸入姓名觸發 `GET /api/employees/search?name=`（debounce）。
- 結果選項顯示姓名、部門、在場狀態（`NTag` 色彩區分在場／不在場）；無結果時顯示 `NEmpty`。
- 點選結果：
  1. 若該員工座位所在樓層非當前 → `floorsStore.selectFloor(...)` 切換。
  2. `map.setView([seat.y, seat.x], targetZoom)`。
  3. 高亮該座位標記（暫時放大或加外框）。
  4. 若 `seat === null` → 以 `useMessage().warning()` 提示「該員工尚未指派座位」。

### 步驟 9：打卡 UI（員工）

- 於 App 頂部列或地圖控制區放「上班打卡 / 下班打卡」按鈕（`NButton`，依在場狀態切換 `type="primary"` / `type="warning"` 與文字標籤）。
- 載入時 `GET /api/attendance/me` 決定按鈕狀態（已在場顯示「下班打卡」）；載入中以 `NSpin` 覆蓋按鈕。
- 點擊 → `POST /api/attendance/check-in|check-out` → 成功後：
  - 更新自身狀態。
  - 重新拉當前樓層座位（或局部更新自己的座位標記顏色），讓地圖即時反映。

### 步驟 10：在場狀態即時更新（輪詢）

- 於 `SeatMap.vue` 設定 `setInterval`（如每 15~30 秒）重新拉 `GET /api/floors/{currentFloorId}/seats`，比對差異更新標記顏色。
- 切換樓層/卸載時清除 interval。
- 視覺更新僅改變標記顏色，避免整圖重繪造成閃爍（可保留 marker，僅更新 icon class）。

### 步驟 11：載入與錯誤狀態

- 樓層無底圖（`mapStatus !== 'Ready'`）：以 `NAlert`（`type="warning"` 或 `type="error"`）顯示提示（「底圖尚未就緒 / 轉檔中 / 轉檔失敗」）；`Processing` 狀態搭配 `NSpin`；仍可顯示座位於空白底。
- 無任何樓層：以 `NEmpty` 搭配提示文字告知管理者先建立樓層。
- API 錯誤：以 `useMessage().error()` 顯示 toast 通知。

### 步驟 12：RWD 與觸控

- 地圖容器自適應視窗大小（`100%` 高寬，監聽 resize → `map.invalidateSize()`）。
- 確認觸控裝置雙指縮放/單指拖曳可用（Leaflet 預設）。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `FrontEnd/src/views/MapView.vue` | 新增（地圖頁容器） |
| `FrontEnd/src/components/map/SeatMap.vue` | 新增 |
| `FrontEnd/src/components/map/SeatPopup.vue` | 新增 |
| `FrontEnd/src/components/map/EmployeeSearch.vue` | 新增 |
| `FrontEnd/src/components/map/seatMarkers.ts` | 新增（icon/標記工具） |
| `FrontEnd/src/components/CheckInButton.vue` | 新增（打卡） |
| `FrontEnd/src/stores/floors.ts` | 新增 |
| `FrontEnd/src/stores/attendance.ts` | 新增（自身狀態） |
| `FrontEnd/src/router/index.ts` | 修改（`/` → MapView） |
| `FrontEnd/src/assets/*.css` | 新增座位標記樣式 |

---

## 驗收條件（DoD）

- [ ] 地圖以 `CRS.Simple` 載入當前樓層 Tile 底圖，縮放/平移正常。
- [ ] 觸控裝置可雙指縮放、單指拖曳。
- [ ] 「重置視角」可回到底圖預設範圍。
- [ ] 左上角顯示目前樓層名稱；右側箭頭可輪播切換至下一樓層，並重載底圖與座位。
- [ ] 座位標記顯示頭像（無則姓名首字），在場為綠、不在場為灰。
- [ ] 未指派座位以 `EventSeatFilled` 座椅圖示顯示為空座位（淺色虛線外框）。
- [ ] 點擊座位顯示姓名、部門浮窗；空座位顯示未指派。
- [ ] 員工搜尋可定位：自動切到對應樓層並置中座位、高亮。
- [ ] 員工可上/下班打卡，打卡後自己的座位顏色即時更新。
- [ ] 輪詢機制使他人打卡後，在數十秒內反映於地圖（顏色變化）。
- [ ] 樓層底圖未就緒/轉檔失敗時有明確提示，不致白畫面。
