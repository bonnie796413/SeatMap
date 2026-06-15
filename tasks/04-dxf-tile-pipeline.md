# 04 — DXF 上傳與 Tile 轉檔

## 模組目標

實作管理者上傳 `.dxf` 底圖後，後端以 GDAL CLI 子行程自動轉檔為 XYZ 圖磚（Tile），
存於 Fly.io persistent volume，並由後端以靜態路由提供給前端 Leaflet（`CRS.Simple`）載入。
涵蓋上傳、轉檔流程、狀態追蹤、錯誤處理、Tile 靜態服務與清理。

對應規格：1.1 底圖上傳、DXF 上傳流程、DXF 解析規則；SA 文件「底圖處理流程」「底圖 Tile 架構」。

## 前置相依

- **01 資料庫**（`FloorMap` 實體）。
- **02 身分驗證**（上傳為管理者操作）。
- **03 樓層管理**（Tile 掛在既有樓層；本模組實作模組 03 預留的 `ITileStorage`）。

---

## 轉檔管線（與 SA 文件一致）

```
管理者上傳 .dxf
       ↓ (ASP.NET Core 接收，存原始檔)
ogr2ogr        解析 DXF 向量 →（GeoJSON/Shapefile 中間格式，僅 Model Space）
       ↓
gdal_rasterize 柵格化為 GeoTIFF（設定解析度 / 像素尺寸）
       ↓
gdal2tiles     切割為 XYZ Tile（-p raster，CRS.Simple 相容），輸出 z/x/y.png
       ↓
存入 volume：/data/tiles/{floorId}/{z}/{x}/{y}.png
       ↓
後端靜態路由 /tiles/... → Leaflet TileLayer 載入
```

> 重點：`gdal2tiles` 使用 `--profile=raster`（非地理投影），輸出符合 Leaflet `CRS.Simple` 的像素座標 Tile。

---

## 設計重點

- **GDAL 來源**：打包進後端 Docker image（模組 12 的 Dockerfile 安裝 `gdal-bin`），後端以 `System.Diagnostics.Process` 呼叫 CLI。
- **非同步**：轉檔耗時，上傳後立即回 202，背景工作執行轉檔，完成後以 **SignalR** 推送結果通知前端。
- **儲存根目錄**：以設定 `Tiles:RootPath`（本機 `./_data/tiles`、容器 `/data/tiles`）。
- **解析度 / zoom 上限**：可設定（SA 文件提醒避免過度放大模糊）；預設輸出像素寬高與 `MaxZoom` 寫入 `FloorMap`。
- **bounds**：以柵格像素尺寸計算 Leaflet bounds `[[0,0],[height,width]]`，存 `FloorMap.BoundsJson` 供前端。

---

## 詳細執行步驟

### 步驟 1：設定與 Options

1. 新增 `BackEnd/Options/GdalOptions.cs`：
   - `Ogr2OgrPath`、`GdalRasterizePath`、`Gdal2TilesPath`（預設假設在 PATH，值為 `ogr2ogr` 等）。
   - `WorkingTempPath`（中間檔暫存）。
   - `RasterPixelSize` 或 `OutputWidth`（柵格化解析度控制）。
   - `MinZoom`、`MaxZoom`。
   - `TimeoutSeconds`（單一程序逾時保護）。
2. 新增 `BackEnd/Options/TilesOptions.cs`：`RootPath`、`PublicBasePath`（`/tiles`）。
3. `appsettings.json` 補上 `Gdal`、`Tiles` 區段；容器路徑於模組 12 以環境變數覆寫。

### 步驟 2：上傳驗證

- 接收 `multipart/form-data`，欄位：`file`（.dxf）。
- 驗證：
  - 副檔名 `.dxf`（不分大小寫）。
  - 檔案大小上限（設定，如 50MB）。
  - 內容非空。
- 對應樓層存在，否則 404。
- 一樓層僅一份底圖：若已有 `FloorMap` 且狀態為 `Processing`，回 409（避免重複觸發）；若為 `Ready/Failed`，允許重新上傳（覆蓋流程見步驟 7）。

### 步驟 3：原始檔儲存

- 存到 `{RootPath}/../dxf/{floorId}/{guid}.dxf`（與 tiles 分開）。
- 寫入/更新 `FloorMap`：`OriginalDxfPath`、`Status=Pending`、`UpdatedAt`。

### 步驟 4：GDAL 執行封裝

新增 `BackEnd/Services/GdalRunner.cs`：
- 通用方法 `RunAsync(string exe, string[] args, CancellationToken)`：
  - 使用 `ProcessStartInfo`（`RedirectStandardError/Output`、`UseShellExecute=false`）。
  - 套用 `TimeoutSeconds`；逾時則 kill 並丟例外。
  - 回傳 exit code + stderr；非 0 視為失敗。
- 三個包裝方法：
  1. `DxfToVectorAsync(dxf, outVector)`：
     `ogr2ogr -f GeoJSON {out} {dxf} -sql "SELECT * FROM entities"`（限制 Model Space；必要時以圖層過濾 LINE/LWPOLYLINE/TEXT/MTEXT）。
  2. `VectorToGeoTiffAsync(vector, outTiff)`：
     `gdal_rasterize -burn 0 -burn 0 -burn 0 -init 255 -ts {W} {H} ...`（黑線白底；尺寸依解析度）。
     - 需先以 `ogrinfo`/`gdalinfo` 取得範圍（extent）以計算像素尺寸與保持比例。
  3. `GeoTiffToTilesAsync(tiff, outDir)`：
     `gdal2tiles --profile=raster -z {min}-{max} -w none {tiff} {outDir}`（`-w none` 不產生 HTML viewer）。

> 註：實際參數需依首份真實 DXF 微調（線寬、底色、留白）。步驟需保留可調設定。

### 步驟 5：轉檔協調服務

新增 `BackEnd/Services/TileConversionService.cs`：
- `ConvertAsync(int floorId)`：
  1. 設 `Status=Processing`。
  2. 建暫存工作目錄。
  3. 依序呼叫 GdalRunner 三步驟。
  4. 計算 bounds、寬高、zoom，寫入 `FloorMap`。
  5. 將輸出 Tile 移至 `{RootPath}/{floorId}/`（先寫暫存再原子搬移，避免半成品被前端讀到）。
  6. 設 `Status=Ready`、`UpdatedAt`；清理暫存。
  7. 任一步失敗 → `Status=Failed`、`ErrorMessage=stderr 摘要`，清理暫存，記 log。

### 步驟 6：背景執行

- 採 .NET `Channel<int>` + `BackgroundService`（`TileConversionWorker`）佇列模式：
  - 上傳 endpoint 將 `floorId` 入列後立即回 202。
  - Worker 逐一取出呼叫 `TileConversionService.ConvertAsync`。
  - 同一時間限制併發數（如 1～2），避免資源耗盡（Fly.io 機器資源有限）。

### 步驟 7：SignalR 即時通知

轉檔完成（`Ready` 或 `Failed`）後以 **SignalR** 主動推送結果給前端。

#### 後端

新增 `BackEnd/Hubs/TileConversionHub.cs`：

```csharp
public class TileConversionHub : Hub
{
    // 前端連線後呼叫此方法加入 floor 群組
    public async Task JoinFloorGroup(int floorId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"floor-{floorId}");

    public async Task LeaveFloorGroup(int floorId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"floor-{floorId}");
}
```

`TileConversionService.ConvertAsync` 於 `Status=Ready` 或 `Status=Failed` 寫入資料庫後，透過注入的 `IHubContext<TileConversionHub>` 推送：

```csharp
await _hubContext.Clients
    .Group($"floor-{floorId}")
    .SendAsync("TileConversionCompleted", new
    {
        floorId,
        status,          // "Ready" | "Failed"
        errorMessage     // null 或失敗摘要
    });
```

`Program.cs` 新增：

```csharp
builder.Services.AddSignalR();
// ...
app.MapHub<TileConversionHub>("/hubs/tile-conversion");
```

> CORS 設定須允許前端 origin（開發期 `http://localhost:5173`），且啟用 `AllowCredentials()`，因 SignalR WebSocket 需要。

#### 前端

安裝 `@microsoft/signalr`。

上傳請求成功（收到 202）後：

1. 建立 `HubConnection`，連線至 `/hubs/tile-conversion`。
2. 呼叫 `connection.invoke("JoinFloorGroup", floorId)` 加入對應群組。
3. 監聽 `TileConversionCompleted` 事件：

```ts
connection.on("TileConversionCompleted", ({ floorId, status, errorMessage }) => {
    if (status === "Ready") {
        // 重新取得 FloorMap 中繼資料（含 tileUrlTemplate、bounds 等），更新 UI
    } else {
        // 顯示 errorMessage
    }
    connection.invoke("LeaveFloorGroup", floorId);
});
```

4. 通知處理完畢後可離開群組並停止連線（或保持連線供後續使用）。

> 頁面初次載入時仍應呼叫 `GET /api/floors/{floorId}/map` 取得當前狀態與中繼資料（tileUrlTemplate、bounds 等），以應對頁面載入時轉檔已完成、尚未建立 SignalR 連線的情境。

### 步驟 8：重新上傳 / 覆蓋

- 重新上傳同樓層底圖：
  1. 轉檔成功後再覆蓋舊 Tile 目錄（先輸出到臨時 dir，成功後 swap）。
  2. 避免轉檔中前端讀到混合新舊磚。
- 對應 SA 文件「更新底圖時需重新執行轉換並覆蓋舊 Tile」。

### 步驟 9：Tile 靜態服務

於 `Program.cs`：
- 以 `UseStaticFiles` 額外掛載：
  ```
  app.UseStaticFiles(new StaticFileOptions {
      FileProvider = new PhysicalFileProvider(tilesRootAbsolute),
      RequestPath = "/tiles",
      // 設定快取標頭（Tile 不常變，可 Cache-Control: public, max-age）
  });
  ```
- 確保 `.png` 已在預設 content-type 對應內。
- Tile 為公開資源（地圖底圖），不需授權即可載入（簡化前端 TileLayer）。

### 步驟 10：實作 ITileStorage（供模組 03 清理）

- `BackEnd/Services/TileStorage.cs` 實作 `ITileStorage`：
  - `DeleteFloorTilesAsync(floorId)`：刪 `{RootPath}/{floorId}` 與對應 dxf 原檔。
  - 刪樓層時由 `FloorService` 呼叫。

### 步驟 11：上傳與狀態 Endpoints

新增 `BackEnd/Endpoints/FloorMapEndpoints.cs`，掛 `/api/floors/{floorId}/map`：

- `POST /api/floors/{floorId}/map`（AdminOnly，multipart）：上傳 DXF → 入列 → 202 + `{status:"Processing"}`。
- `GET /api/floors/{floorId}/map`（需登入）：回 `FloorMap` 中繼資料（status、bounds、minZoom、maxZoom、tileUrlTemplate）。
  - `tileUrlTemplate` 例：`/tiles/{floorId}/{z}/{x}/{y}.png`（前端直接給 Leaflet）。
  - 此 endpoint 供**頁面初次載入**使用；轉檔進行中的結果通知由 SignalR 推送（見步驟 7）。
- `DELETE /api/floors/{floorId}/map`（AdminOnly）：移除底圖與 Tile（保留樓層）。

### 步驟 12：本機開發替代

- 本機若未裝 GDAL，提供設定旗標 `Gdal:Enabled=false`：跳過實際轉檔，直接標記 `Ready` 並放入一張預先準備的測試 Tile 集，以利前端開發不被卡住。

---

## 涉及檔案

| 檔案 | 動作 |
|------|------|
| `BackEnd/Options/GdalOptions.cs` | 新增 |
| `BackEnd/Options/TilesOptions.cs` | 新增 |
| `BackEnd/Hubs/TileConversionHub.cs` | 新增 |
| `BackEnd/Services/GdalRunner.cs` | 新增 |
| `BackEnd/Services/TileConversionService.cs` | 新增 |
| `BackEnd/Services/TileConversionWorker.cs`（BackgroundService） | 新增 |
| `BackEnd/Services/TileStorage.cs`（實作 ITileStorage） | 新增 |
| `BackEnd/Dtos/Floors/FloorMapResponse.cs` | 新增 |
| `BackEnd/Endpoints/FloorMapEndpoints.cs` | 新增 |
| `BackEnd/Program.cs` | 修改（靜態檔、DI、背景服務、SignalR、endpoint） |
| `BackEnd/appsettings.json` | 加 `Gdal`、`Tiles` |
| `FrontEnd/src/composables/useTileConversion.ts` | 新增（SignalR 連線與群組管理） |
| `BackEnd/Dockerfile`（模組 12 建立，需含 `gdal-bin`） | 關聯 |

---

## API 合約（本模組）

| Method | Path | 授權 | 請求 | 回應 |
|--------|------|------|------|------|
| POST | `/api/floors/{floorId}/map` | Admin | multipart `file=.dxf` | 202 `{status,floorId}` |
| GET | `/api/floors/{floorId}/map` | 需登入 | — | `{floorId,status,minZoom,maxZoom,bounds,tileUrlTemplate,errorMessage?}` |
| DELETE | `/api/floors/{floorId}/map` | Admin | — | 204 |

靜態：`GET /tiles/{floorId}/{z}/{x}/{y}.png`（公開）。

---

## 驗收條件（DoD）

- [ ] 管理者上傳合法 `.dxf`，回 202 且 `FloorMap.Status` 變為 `Processing`。
- [ ] 背景轉檔完成後 `Status` 變 `Ready`，`/tiles/{floorId}/{z}/{x}/{y}.png` 可取得圖磚。
- [ ] 轉檔完成（Ready 或 Failed）後，前端透過 SignalR 即時收到 `TileConversionCompleted` 事件，無需輪詢。
- [ ] `GET /api/floors/{floorId}/map` 回傳正確 `tileUrlTemplate`、`bounds`、`minZoom/maxZoom`（供初次載入使用）。
- [ ] 上傳非 `.dxf` 或解析失敗 → `Status=Failed` 且 `errorMessage` 有內容，SignalR 事件攜帶錯誤訊息，前端可顯示錯誤（呼應規格「解析失敗顯示錯誤並擋上傳」）。
- [ ] 重新上傳底圖會覆蓋舊 Tile，且轉檔期間前端不會載到半成品。
- [ ] 刪除樓層或刪除底圖會清掉對應 Tile 目錄與原始 DXF。
- [ ] GDAL 以子行程呼叫，逾時會被中止並標記失敗（不會無限卡住）。
- [ ] `Gdal:Enabled=false` 時本機可略過轉檔以利前端開發。
- [ ] 僅使用 Model Space 資料（多 Layout 時固定取模型空間）。
