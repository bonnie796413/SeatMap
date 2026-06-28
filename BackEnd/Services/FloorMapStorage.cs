using BackEnd.Options;
using Microsoft.Extensions.Options;

namespace BackEnd.Services;

/// <summary>
/// 底圖檔案的路徑計算與清理。GeoJSON 存於 {Root}/maps/{floorId}.geojson，
/// 原始 DXF 存於 {Root}/dxf/{floorId}/。
/// </summary>
public class FloorMapStorage(IOptions<MapStorageOptions> opts, ILogger<FloorMapStorage> logger) : IFloorMapStorage
{
    private readonly MapStorageOptions _opts = opts.Value;

    public string RootAbsolute => Path.GetFullPath(_opts.RootPath);
    public string MapsDirAbsolute => Path.Combine(RootAbsolute, "maps");
    public string DxfRootAbsolute => Path.Combine(RootAbsolute, "dxf");

    public string GeoJsonPath(Guid floorId) => Path.Combine(MapsDirAbsolute, $"{floorId}.geojson");
    public string DxfDir(Guid floorId) => Path.Combine(DxfRootAbsolute, floorId.ToString());

    public Task DeleteFloorMapAsync(Guid floorId)
    {
        var geo = GeoJsonPath(floorId);
        if (File.Exists(geo))
        {
            File.Delete(geo);
            logger.LogInformation("已刪除樓層 {FloorId} 的 GeoJSON 底圖", floorId);
        }

        var dxfDir = DxfDir(floorId);
        if (Directory.Exists(dxfDir))
            Directory.Delete(dxfDir, recursive: true);

        return Task.CompletedTask;
    }
}
