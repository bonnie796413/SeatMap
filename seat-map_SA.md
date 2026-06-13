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
| 前端 | Leaflet | 輕量級開源 JavaScript 地圖函式庫，用於在瀏覽器中渲染互動式地圖，支援圖層、標記、多邊形等地圖元素 |
| 轉檔工具 | GDAL | 開源地理資料轉換工具集，透過命令列（`ogr2ogr`）將 DXF 解析後轉換，再切割為地圖 Tile，供 Leaflet CRS.Simple 模式載入底圖 |

---

## 底圖處理流程（DXF → Tile）

管理者上傳 DXF 後，後端自動觸發預設 CLI 腳本執行轉換：

```
管理者上傳 .dxf
       ↓
後端接收檔案，觸發預設 CLI 腳本
       ↓
GDAL ogr2ogr 解析 DXF 向量資料
       ↓
柵格化為 GeoTIFF
       ↓
切割為 Tile
       ↓
Leaflet CRS.Simple 載入 Tile 作為底圖
```

---

## 功能模組對應技術

| 功能 | 技術 |
|------|------|
| 底圖上傳與轉換 | GDAL CLI、ASP.NET Core Web API |
| 座位幾何座標儲存 | PostGIS (Point) + NetTopologySuite |
| 座位地圖顯示 | Leaflet（CRS.Simple + Tile Layer） |
| 多樓層管理 | PostgreSQL + EF Core |
| 打卡狀態更新 | ASP.NET Core Web API |
| 員工搜尋定位 | Leaflet `setView` |

---

## 底圖 Tile 架構

- Tile 以 **XYZ 靜態檔案**方式提供：GDAL 切割後存至磁碟，由 Nginx 靜態伺服器依路徑提供給前端 Leaflet 載入
- DXF 轉換流程：`ogr2ogr`（解析向量）→ `gdal_rasterize`（柵格化為 GeoTIFF）→ `gdal2tiles`（切割為 XYZ Tile）
- 底圖以圖片形式呈現，清晰度取決於輸出解析度；需預先設定適當解析度與 zoom 層級上限，避免過度放大造成模糊
- 更新底圖時需重新執行轉換流程並覆蓋舊 Tile
