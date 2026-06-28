using OSGeo.OGR;

namespace BackEnd.Services;

/// <summary>
/// 以 MaxRev.Gdal（in-process OGR）將 DXF 解析轉為 GeoJSON。
/// GDAL native 初始化（GdalBase.ConfigureAll / Ogr.RegisterAll）於應用啟動時於 Program.cs 執行一次。
/// DXF 無地理座標系，OGR 不做投影轉換，GeoJSON 直接保留 DXF 平面座標供 Leaflet CRS.Simple 使用。
/// </summary>
public class DxfToGeoJsonConverter(ILogger<DxfToGeoJsonConverter> logger)
{
    /// <summary>解析 DXF 並輸出 GeoJSON，回傳寫出的 feature 數。</summary>
    public long Convert(string dxfPath, string outGeoJsonPath)
    {
        using var src = Ogr.Open(dxfPath, 0)
            ?? throw new InvalidOperationException("無法以 OGR 開啟 DXF（檔案可能損毀或非合法 DXF）");

        var driver = Ogr.GetDriverByName("GeoJSON")
            ?? throw new InvalidOperationException("GDAL 缺少 GeoJSON driver");

        if (File.Exists(outGeoJsonPath))
            File.Delete(outGeoJsonPath);

        var outDir = Path.GetDirectoryName(outGeoJsonPath);
        if (!string.IsNullOrEmpty(outDir))
            Directory.CreateDirectory(outDir);

        using var dst = driver.CreateDataSource(outGeoJsonPath, Array.Empty<string>())
            ?? throw new InvalidOperationException("無法建立 GeoJSON 輸出資料來源");

        long featureCount = 0;
        var layerCount = src.GetLayerCount();
        for (int i = 0; i < layerCount; i++)
        {
            var layer = src.GetLayerByIndex(i);
            if (layer is null) continue;

            // OGR DXF driver 預設讀模型空間 (ENTITIES)；整層複製到 GeoJSON。
            // 線條 (LINE/LWPOLYLINE) → LineString；文字 (TEXT/MTEXT) → Point + 文字屬性。
            dst.CopyLayer(layer, layer.GetName(), Array.Empty<string>());
            featureCount += layer.GetFeatureCount(1);
        }

        dst.FlushCache();
        logger.LogInformation("DXF→GeoJSON 完成：{Path}（{Count} features）", outGeoJsonPath, featureCount);
        return featureCount;
    }
}
