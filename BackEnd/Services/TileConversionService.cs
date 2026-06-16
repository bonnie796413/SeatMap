using BackEnd.Data;
using BackEnd.Entities;
using BackEnd.Hubs;
using BackEnd.Options;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackEnd.Services;

public class TileConversionService(
    AppDbContext db,
    GdalRunner gdal,
    IOptions<GdalOptions> gdalOpts,
    IOptions<TilesOptions> tilesOpts,
    IHubContext<TileConversionHub> hub,
    ILogger<TileConversionService> logger)
{
    private readonly GdalOptions _gdal = gdalOpts.Value;
    private readonly TilesOptions _tiles = tilesOpts.Value;

    public async Task ConvertAsync(Guid floorId, CancellationToken ct = default)
    {
        var map = await db.FloorMaps.FirstOrDefaultAsync(m => m.FloorId == floorId, ct);
        if (map is null)
        {
            logger.LogWarning("找不到樓層 {FloorId} 的 FloorMap，跳過轉檔", floorId);
            return;
        }

        map.Status = FloorMapStatus.Processing;
        map.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // GDAL disabled → 直接標記 Ready（本機開發模式）
        if (!_gdal.Enabled)
        {
            map.Status = FloorMapStatus.Ready;
            map.MinZoom = _gdal.MinZoom;
            map.MaxZoom = _gdal.MaxZoom;
            map.Width = _gdal.OutputWidth;
            map.Height = _gdal.OutputWidth;
            map.BoundsJson = $"[[0,0],[{_gdal.OutputWidth},{_gdal.OutputWidth}]]";
            map.TileDirectory = Path.Combine(_tiles.RootPath, floorId.ToString());
            map.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await NotifyAsync(floorId, "Ready", null);
            return;
        }

        var tempDir = Path.Combine(_gdal.WorkingTempPath, floorId.ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var vectorPath = Path.Combine(tempDir, "vector.geojson");
            var tiffPath = Path.Combine(tempDir, "raster.tiff");
            var tempTileDir = Path.Combine(tempDir, "tiles");

            await gdal.DxfToVectorAsync(map.OriginalDxfPath, vectorPath, ct);
            var (w, h) = await gdal.VectorToGeoTiffAsync(vectorPath, tiffPath, _gdal.OutputWidth, ct);
            await gdal.GeoTiffToTilesAsync(tiffPath, tempTileDir, _gdal.MinZoom, _gdal.MaxZoom, ct);

            // 原子搬移
            var finalTileDir = Path.Combine(_tiles.RootPath, floorId.ToString());
            if (Directory.Exists(finalTileDir))
                Directory.Delete(finalTileDir, recursive: true);
            Directory.Move(tempTileDir, finalTileDir);

            map.Status = FloorMapStatus.Ready;
            map.Width = w;
            map.Height = h;
            map.MinZoom = _gdal.MinZoom;
            map.MaxZoom = _gdal.MaxZoom;
            map.BoundsJson = $"[[0,0],[{h},{w}]]";
            map.TileDirectory = finalTileDir;
            map.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await NotifyAsync(floorId, "Ready", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "樓層 {FloorId} 轉檔失敗", floorId);
            map.Status = FloorMapStatus.Failed;
            map.ErrorMessage = ex.Message;
            map.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await NotifyAsync(floorId, "Failed", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private Task NotifyAsync(Guid floorId, string status, string? errorMessage) =>
        hub.Clients.Group($"floor-{floorId}").SendAsync("TileConversionCompleted", new
        {
            floorId,
            status,
            errorMessage
        });
}
