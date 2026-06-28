# 04 — DXF 上傳與 GeoJSON 轉檔

> **方案變更紀錄**：本模組原採「DXF → 柵格化 → XYZ 圖磚（GDAL CLI）」方案，因
> `gdal_rasterize`／`gdal2tiles` 兩步轉檔穩定度過低而廢止。現改為
> **DXF → GeoJSON 向量**，回歸 `seat-map.md` 1.1「轉換為可在瀏覽器顯示的向量圖層」之原始需求。

## 模組目標

實作管理者上傳 `.dxf` 底圖後，後端以 **`MaxRev.Gdal`（GDAL 的 .NET 原生繫結 NuGet）**
在程式內（in-process）用 OGR 解析 DXF，**同步**轉檔為 **GeoJSON 向量**檔，存於磁碟，
並由後端以靜態路由 `/maps/{floorId}.geojson` 提供給前端 Leaflet（`CRS.Simple`）以
`L.geoJSON` 向量圖層載入。涵蓋上傳、同步轉檔、座標處理、錯誤處理、
GeoJSON 靜態服務與清理。

對應規格：1.1 底圖上傳（向量圖層）、DXF 上傳流程、DXF 解析規則；SA 文件「底圖處理流程」「底圖向量架構」。

> **不再採用**：`gdal_rasterize`、`gdal2tiles`、XYZ 圖磚、tile 靜態服務、背景佇列、SignalR 通知。

## 前置相依

- **01 資料庫**（`FloorMap` 實體；欄位已配合本方案調整，見模組 01）。
- **02 身分驗證**（上傳為管理者操作）。
- **03 樓層管理**（底圖掛在既有樓層；本模組實作模組 03 預留的 `IFloorMapStorage`）。

---

## 轉檔管線（與 SA 文件一致）

```
管理者上傳 .dxf
       ↓ (ASP.NET Core 接收，存原始檔)
GdalBase.ConfigureAll()  一次性初始化 MaxRev.Gdal native libs（應用啟動時）
       ↓
Gdal.OpenEx(dxf, OF_VECTOR)        以 OGR DXF driver 開啟（僅 Model Space）
       ↓
Gdal.VectorTranslate("-f GeoJSON") in-process 等同 ogr2ogr，輸出 .geojson
       ↓
寫入 FloorMap（GeoJsonPath / Status，同步、單一請求內完成）
       ↓
存檔：{RootPath}/maps/{floorId}.geojson
       ↓
後端靜態路由 /maps/{floorId}.geojson → 前端 L.geoJSON 向量圖層載入
```

> 重點：DXF 無地理座標系（CRS），`VectorTranslate` **不做投影轉換**，GeoJSON 直接保留
> DXF 平面座標，供 Leaflet `CRS.Simple` 使用（座標對齊細節見「設計重點」）。

---

## 設計重點

- **GDAL 來源**：以 NuGet `MaxRev.Gdal.*` 提供，**native library 隨套件帶入**，
  **不需** Docker `apt install gdal-bin`、**不需** `System.Diagnostics.Process` 呼叫外部 CLI。
  後端直接以 `OSGeo.GDAL` / `OSGeo.OGR` 託管 API 操作。
- **同步處理**：DXF → GeoJSON 僅 OGR 解析一步，耗時為秒級，於上傳請求內**同步完成**並回傳結果。
  **移除** 背景佇列（`Channel`/`BackgroundService`）與 **SignalR** 即時通知（連同前端 `@microsoft/signalr`、CORS `AllowCredentials` 一併移除）。
- **儲存**：GeoJSON 以**檔案**儲存於 `MapStorage:RootPath`（本機 `./_data`、容器 `/data`）下 `maps/{floorId}.geojson`；原始 DXF 存 `dxf/{floorId}/{guid}.dxf`。
- **座標對齊**（關鍵）：
  - DXF 為 CAD 平面座標（Y 軸向上），與 Leaflet `CRS.Simple` 的緯度方向一致，**不需翻轉 Y**。
  - GeoJSON 座標為 `[x, y]`；`L.geoJSON` 在 `CRS.Simple` 下預設以 `latLng(y, x)` 繪製。
  - 座位 `Seat.Location` 沿用**相同的 DXF 平面座標系**（前端在底圖上點擊新增座位時，將 Leaflet 座標換回該座標存入；見模組 05/11），故座位與底圖天然對齊。
- **縮放/定位**：向量圖層可無限縮放不模糊，無預切層級；初始定位與切換樓層皆以前端 `fitBounds(layer.getBounds())` 自動完成，**不在 DB 存 `MinZoom`/`MaxZoom`**（縮放為純前端互動，必要時於前端設全域上下限即可）。
- **本機開發**：`MaxRev.Gdal` 自帶各平台 native，本機（Windows/macOS/Linux）與容器皆可直接執行，**移除**舊的 `Gdal:Enabled=false` 跳過旗標。

---

## 詳細執行步驟

### 步驟 1：NuGet 套件與初始化

1. 於 `BackEnd/BackEnd.csproj` 加入：
   - `MaxRev.Gdal.Core`（GDAL/OGR 託管 API：`OSGeo.GDAL`、`OSGeo.OGR`、`OSGeo.OSR`）。
   - 對應平台 runtime 套件：開發機 `MaxRev.Gdal.WindowsRuntime.Minimal`、Linux 容器 `MaxRev.Gdal.LinuxRuntime.Minimal`（兩者皆列，依 RID 還原；`Minimal` 版即足夠，僅需向量/DXF/GeoJSON driver）。
2. 應用啟動時**一次性**初始化（`Program.cs`，DI 建置前）：
   ```csharp
   MaxRev.Gdal.Core.GdalBase.ConfigureAll();   // 設定 native libs 與 GDAL_DATA / PROJ
   OSGeo.OGR.Ogr.RegisterAll();                // 註冊 OGR driver（含 DXF、GeoJSON）
   ```
   > 實際初始化呼叫以 `MaxRev.Gdal` 套件當前版本 README 為準（核心為 `GdalBase.ConfigureAll()`）。

### 步驟 2：設定與 Options

1. 新增 `BackEnd/Options/MapStorageOptions.cs`：
   - `RootPath`（本機 `./_data`、容器 `/data`，模組 12 以環境變數覆寫）。
   - `PublicBasePath`（`/maps`）。
2. 新增 `BackEnd/Options/GeoJsonConversionOptions.cs`：
   - `MaxUploadBytes`（DXF 大小上限，如 50MB）。
   - `TimeoutSeconds`（轉檔逾時保護；以 `CancellationToken` 套用於轉檔工作）。
3. `appsettings.json` 補上 `MapStorage`、`GeoJsonConversion` 區段；容器路徑於模組 12 以環境變數覆寫。

### 步驟 3：上傳驗證

- 接收 `multipart/form-data`，欄位：`file`（.dxf）。
- 驗證：
  - 副檔名 `.dxf`（不分大小寫）。
  - 檔案大小上限（`MaxUploadBytes`）。
  - 內容非空。
- 對應樓層存在，否則 404。
- 一樓層僅一份底圖：允許重新上傳覆蓋（覆蓋流程見步驟 7）。同步處理下無「進行中」競態，不需 409 鎖。

### 步驟 4：原始檔儲存

- 存到 `{RootPath}/dxf/{floorId}/{guid}.dxf`（與 geojson 分開）。
- 寫入/更新 `FloorMap`：`OriginalDxfPath`、`Status=Pending`、`UpdatedAt`。

### 步驟 5：GDAL/OGR 轉檔封裝

新增 `BackEnd/Services/DxfToGeoJsonConverter.cs`（或 `IGeoJsonConverter` + 實作）：
- 方法 `Convert(string dxfPath, string outGeoJsonPath)` → 回傳 `ConversionResult { FeatureCount }`（用於驗證 GeoJSON 非空）：
  1. `using var srcDs = Gdal.OpenEx(dxfPath, (uint)GdalConst.OF_VECTOR, null, null, null);`
     - `null` → 開啟失敗（非合法 DXF），丟例外交由步驟 6 標記 `Failed`。
  2. 轉檔（in-process 等同 `ogr2ogr -f GeoJSON`）：
     ```csharp
     var opts = new GDALVectorTranslateOptions(new[] {
         "-f", "GeoJSON",
         // 僅 Model Space：OGR DXF driver 預設讀 ENTITIES（模型空間）
         // 可選過濾線條/文字，預設保留全部幾何（見「DXF 解析規則」）
     });
     using var outDs = Gdal.wrapper_GDALVectorTranslateDestName(
         outGeoJsonPath, srcDs, opts, null, null);
     outDs.FlushCache();
     ```
     > 方法簽章（`Gdal.VectorTranslate` / `Gdal.wrapper_GDALVectorTranslateDestName`）依 GDAL C# bindings 版本為準。
  3. 可選：開啟輸出 GeoJSON 確認 feature 數非 0（`ds.GetLayerByIndex(0).GetFeatureCount(1)`），作為「解析成功但無有效幾何」的防呆。
- DXF 無 CRS → 不投影；GeoJSON 保留原始平面座標。範圍（bounds）不在後端計算或儲存，由前端 `layer.getBounds()` 取得。

> 註：實際 driver 行為（圖層 `Layer`/`SubClasses`/`Text` 欄位、`DXF_INLINE_BLOCKS` 等 open option）依首份真實 DXF 微調。步驟需保留可調設定。

### 步驟 6：轉檔協調服務

新增 `BackEnd/Services/FloorMapService.cs`：
- `UploadAndConvertAsync(int floorId, Stream dxf, string fileName, CancellationToken)`：
  1. 驗證樓層存在、檔案合法（步驟 3）。
  2. 存原始 DXF，`Status=Pending`。
  3. 設 `Status=Processing`（同步流程仍寫入，便於失敗時留存狀態）。
  4. 轉檔輸出到**暫存檔** `maps/{floorId}.geojson.tmp`，呼叫 `DxfToGeoJsonConverter.Convert`，套用 `TimeoutSeconds`。
  5. 成功 → 原子搬移覆蓋 `maps/{floorId}.geojson`；寫 `FloorMap`：`GeoJsonPath`、`Status=Ready`、`UpdatedAt`，回傳 meta。
  6. 失敗（OGR 開啟失敗／逾時／例外）→ `Status=Failed`、`ErrorMessage`，清理暫存，記 log，向上拋出供 endpoint 回錯。

### 步驟 7：重新上傳 / 覆蓋

- 重新上傳同樓層底圖：先輸出到 `.tmp`，成功後原子搬移覆蓋舊 `.geojson`。
- 避免轉檔中前端讀到半成品（同步流程窗口極短，仍以暫存→swap 保證原子性）。
- 對應 SA 文件「更新底圖時需重新轉換並覆蓋舊檔」。

### 步驟 8：GeoJSON 靜態服務

於 `Program.cs`：
- 以 `UseStaticFiles` 額外掛載 `maps/` 目錄：
  ```csharp
  app.UseStaticFiles(new StaticFileOptions {
      FileProvider = new PhysicalFileProvider(mapsRootAbsolute),
      RequestPath = "/maps",
      ServeUnknownFileTypes = true,             // .geojson
      DefaultContentType = "application/geo+json",
      // 設定快取標頭（底圖不常變，可 Cache-Control: public, max-age）
  });
  ```
- 或為 `.geojson` 補上 `FileExtensionContentTypeProvider` 對應（`application/geo+json`）。
- 底圖為公開資源（視覺參考），不需授權即可載入（簡化前端載圖）。

### 步驟 9：實作 IFloorMapStorage（供模組 03 清理）

- `BackEnd/Services/FloorMapStorage.cs` 實作 `IFloorMapStorage`：
  - `DeleteFloorMapAsync(floorId)`：刪 `maps/{floorId}.geojson` 與 `dxf/{floorId}` 原檔。
  - 刪樓層時由 `FloorService` 呼叫（模組 03）。

> 介面由 `tile`→`map` 更名：模組 03 原 `ITileStorage.DeleteFloorTilesAsync` → `IFloorMapStorage.DeleteFloorMapAsync`。

### 步驟 10：上傳與狀態 Endpoints

新增 `BackEnd/Endpoints/FloorMapEndpoints.cs`，掛 `/api/floors/{floorId}/map`：

- `POST /api/floors/{floorId}/map`（AdminOnly，multipart）：上傳 DXF → **同步**轉檔 →
  成功回 **200** + `FloorMapResponse`（`status:"Ready"`、`geoJsonUrl`）；
  解析失敗回 **400** + `ProblemDetails`（`Status=Failed`、`errorMessage` 已寫入）。
- `GET /api/floors/{floorId}/map`（需登入）：回 `FloorMap` 中繼資料
  （`status`、`geoJsonUrl`、`errorMessage?`）。
  - `geoJsonUrl` 例：`/maps/{floorId}.geojson`（前端 `fetch` 後交給 `L.geoJSON`）。
- `DELETE /api/floors/{floorId}/map`（AdminOnly）：移除底圖與 GeoJSON（保留樓層）。

---

## DXF 解析規則（對齊規格 1.1）

- 僅使用 **Model Space**：OGR DXF driver 讀取 DXF `ENTITIES` 區段（模型空間），不含圖紙空間 Layout。
- 線條（`LINE`、`LWPOLYLINE`）→ GeoJSON `LineString`/`MultiLineString`；
  文字（`TEXT`、`MTEXT`）→ `Point` feature，文字內容置於屬性（OGR DXF driver 的 `Text` 欄位）。
- feature 屬性保留 `Layer`（圖層名）等供前端分層樣式（線條 vs 文字）。
- 底圖固定完整顯示，僅作視覺參考、不帶業務邏輯。
- 解析失敗（OGR 無法開啟／無有效幾何）→ `Status=Failed` 並回錯，呼應規格「解析失敗顯示錯誤並擋上傳」。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/Options/MapStorageOptions.cs` | 新增 |
| `BackEnd/Options/GeoJsonConversionOptions.cs` | 新增 |
| `BackEnd/Services/DxfToGeoJsonConverter.cs`（OGR in-process 轉檔） | 新增 |
| `BackEnd/Services/FloorMapService.cs`（上傳→同步轉檔協調） | 新增 |
| `BackEnd/Services/FloorMapStorage.cs`（實作 `IFloorMapStorage`） | 新增 |
| `BackEnd/Dtos/Floors/FloorMapResponse.cs` | 新增 |
| `BackEnd/Endpoints/FloorMapEndpoints.cs` | 新增 |
| `BackEnd/Program.cs` | 修改（`GdalBase.ConfigureAll()`、靜態檔 `/maps`、DI、endpoint） |
| `BackEnd/BackEnd.csproj` | 加 `MaxRev.Gdal.*` 套件 |
| `BackEnd/appsettings.json` | 加 `MapStorage`、`GeoJsonConversion` |
| ~~`BackEnd/Hubs/TileConversionHub.cs`~~ | **不建立**（移除 SignalR） |
| ~~`BackEnd/Services/GdalRunner.cs`（CLI 子行程）~~ | **不建立**（改 in-process） |
| ~~`BackEnd/Services/TileConversionWorker.cs`（背景服務）~~ | **不建立**（改同步） |
| ~~`FrontEnd/src/composables/useTileConversion.ts`（SignalR）~~ | **不建立**（移除） |

---

## API 合約（本模組）

| Method | Path | 授權 | 請求 | 回應 |
|--------|------|------|------|------|
| POST | `/api/floors/{floorId}/map` | Admin | multipart `file=.dxf` | 200 `FloorMapResponse`（`status:"Ready"`）／失敗 400 `ProblemDetails` |
| GET | `/api/floors/{floorId}/map` | 需登入 | — | `{floorId,status,geoJsonUrl,errorMessage?}` |
| DELETE | `/api/floors/{floorId}/map` | Admin | — | 204 |

靜態：`GET /maps/{floorId}.geojson`（公開，`application/geo+json`）。

---

## 驗收條件（DoD）

- [ ] 管理者上傳合法 `.dxf`，**同步**轉檔成功，回 200 且 `FloorMap.Status=Ready`。
- [ ] `/maps/{floorId}.geojson` 可取得有效 GeoJSON（含 DXF 線條與文字幾何）。
- [ ] `GET /api/floors/{floorId}/map` 回傳正確 `geoJsonUrl` 與 `status`。
- [ ] 上傳非 `.dxf` 或解析失敗 → 回 400、`Status=Failed` 且 `errorMessage` 有內容，前端可顯示錯誤（呼應規格「解析失敗顯示錯誤並擋上傳」）。
- [ ] 重新上傳底圖會以原子搬移覆蓋舊 GeoJSON，不會讀到半成品。
- [ ] 刪除樓層或刪除底圖會清掉對應 `maps/{floorId}.geojson` 與原始 DXF。
- [ ] 轉檔以 `MaxRev.Gdal` in-process 執行，**不依賴外部 CLI 或 `gdal-bin`**；逾時會被中止並標記失敗。
- [ ] 僅使用 Model Space 資料（多 Layout 時固定取模型空間）。
- [ ] GeoJSON 座標未被重投影，與座位座標同系，前端疊圖對齊正確。
