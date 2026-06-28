# Seat Map — 系統分析文件

## 技術棧

| 分類 | 技術 | 說明 |
|------|------|------|
| 資料庫 | PostgreSQL | 開源關聯式資料庫，穩定性高，支援複雜查詢與 JSON 欄位，作為本專案主要資料儲存層 |
| 資料庫 | PostGIS | PostgreSQL 的地理空間擴充套件，提供幾何型別（Point、Polygon 等）與空間索引、空間查詢函式（如距離、相交判斷） |
| 後端 | ASP.NET Core Web API | 微軟跨平台後端框架，用於建立 RESTful API，處理前端請求與業務邏輯 |
| 後端 | Entity Framework Core | .NET 的 ORM 框架，透過 Code First 模型對應資料庫表格，簡化 SQL 操作 |
| 後端 | NetTopologySuite | .NET 地理空間函式庫，提供幾何物件模型與運算，搭配 EF Core 與 PostGIS 進行空間資料的讀寫與計算 |
| 前端 | Vue + Vite | Vue 為漸進式前端框架，負責元件化 UI 開發；Vite 為高效能建置工具，提供快速的開發伺服器與 HMR |
| 前端 | Naive UI | Vue 3 的 UI 元件庫，提供表單（`n-form`）、Dialog（`n-modal`）、Table（`n-data-table`）、Upload（`n-upload`）、Menu（`n-menu`）等豐富元件，以全域安裝方式（`app.use(naive)`）整合至應用 |
| 前端 | @vicons/material | xicons 系列的 Material Design 圖示集，與 Naive UI 搭配使用；座位標記以 SVG 字串嵌入 Leaflet `L.divIcon` 呈現於地圖上——已指派座位使用 `PersonFilled` 圖示（或員工頭像），未指派空座位使用 `EventSeatFilled` 圖示 |
| 前端 | vue-draggable-plus | 基於 Sortable.js 的 Vue 3 拖曳套件，用於管理後台樓層清單的拖曳排序功能，排序結果提交至後端 `PUT /api/floors/reorder` |
| 前端 | Leaflet | 輕量級開源 JavaScript 地圖函式庫，用於在瀏覽器中渲染互動式地圖，支援圖層、標記、多邊形等地圖元素 |
| 轉檔工具 | GDAL（`MaxRev.Gdal` NuGet） | 開源地理資料轉換工具集；以 `MaxRev.Gdal` 的 .NET 原生繫結（`OSGeo.OGR`）在後端 in-process 將 DXF 以 `VectorTranslate` 轉為 **GeoJSON 向量**，供 Leaflet `CRS.Simple` 以 `L.geoJSON` 載入底圖；不需外部 CLI 子行程或 `gdal-bin` |

---

## 底圖處理流程（DXF → GeoJSON）

管理者上傳 DXF 後，後端在請求內**同步**以 `MaxRev.Gdal` 解析並轉檔：

```
管理者上傳 .dxf
       ↓
後端接收檔案（ASP.NET Core），存原始 DXF
       ↓
MaxRev.Gdal（in-process OGR）開啟 DXF（僅 Model Space）
       ↓
VectorTranslate 轉為 GeoJSON（等同 ogr2ogr -f GeoJSON，不投影）
       ↓
存檔 /maps/{floorId}.geojson，寫入 FloorMap（GeoJsonPath / Status）
       ↓
同步回傳結果（Ready / Failed）；前端 Leaflet CRS.Simple 以 L.geoJSON 載入底圖
```

---

## 功能模組對應技術

| 功能 | 技術 |
|------|------|
| 底圖上傳與轉換 | MaxRev.Gdal（in-process OGR `VectorTranslate`）、ASP.NET Core Web API（同步） |
| 座位幾何座標儲存 | PostGIS (Point) + NetTopologySuite |
| 座位地圖顯示 | Leaflet（CRS.Simple + L.geoJSON 向量圖層） |
| 多樓層管理 | PostgreSQL + EF Core |
| 打卡狀態更新 | ASP.NET Core Web API |
| 員工搜尋定位 | Leaflet `setView` |
| 表單／Dialog／Table 等 UI 元件 | Naive UI |
| 樓層清單拖曳排序 | vue-draggable-plus |
| 圖示顯示（含地圖員工標記） | @vicons/material（已指派 `PersonFilled` / 空座位 `EventSeatFilled`） |

---

## 底圖向量（GeoJSON）架構

- 底圖以 **GeoJSON 檔案**方式提供：`MaxRev.Gdal` 轉檔後存至磁碟（persistent volume），由後端靜態路由 `/maps/{floorId}.geojson` 提供給前端 Leaflet 載入
- DXF 轉換流程：`MaxRev.Gdal` in-process OGR — `Gdal.OpenEx`（開啟 DXF，僅 Model Space）→ `VectorTranslate -f GeoJSON`（解析向量、不投影）→ 存檔（範圍由前端 `getBounds()` 取得，不在後端計算）
- 底圖以**向量**呈現，可無限縮放不失真；無預切層級，前端以 `fitBounds(layer.getBounds())` 自動定位，縮放範圍不存 DB
- 前端以 `L.geoJSON` 渲染：DXF 線條（LINE/LWPOLYLINE）→ `LineString`，文字（TEXT/MTEXT）→ `Point` + 文字標籤
- 更新底圖時重新執行轉換並以原子搬移覆蓋舊 GeoJSON
