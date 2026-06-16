using BackEnd.Options;
using Microsoft.Extensions.Options;

namespace BackEnd.Services;

public class TileStorage(IOptions<TilesOptions> opts, ILogger<TileStorage> logger) : ITileStorage
{
    private readonly string _root = opts.Value.RootPath;

    public Task DeleteFloorTilesAsync(Guid floorId)
    {
        var tileDir = Path.Combine(_root, floorId.ToString());
        if (Directory.Exists(tileDir))
        {
            Directory.Delete(tileDir, recursive: true);
            logger.LogInformation("已刪除樓層 {FloorId} 的 Tile 目錄", floorId);
        }

        // 刪除原始 DXF 目錄（若存在）
        var dxfDir = Path.Combine(Path.GetDirectoryName(_root) ?? ".", "dxf", floorId.ToString());
        if (Directory.Exists(dxfDir))
            Directory.Delete(dxfDir, recursive: true);

        return Task.CompletedTask;
    }
}
