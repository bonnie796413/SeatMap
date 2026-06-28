using BackEnd.Data;
using BackEnd.Dtos.Floors;
using BackEnd.Entities;
using BackEnd.Infrastructure;
using BackEnd.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackEnd.Services;

/// <summary>
/// 底圖上傳與同步轉檔協調：接收 DXF → 存原始檔 → in-process 轉 GeoJSON → 原子搬移 → 更新 FloorMap。
/// </summary>
public class FloorMapService(
    AppDbContext db,
    FloorMapStorage storage,
    DxfToGeoJsonConverter converter,
    IOptions<MapStorageOptions> mapOpts,
    IOptions<GeoJsonConversionOptions> convOpts,
    ILogger<FloorMapService> logger)
{
    private readonly MapStorageOptions _mapOpts = mapOpts.Value;
    private readonly GeoJsonConversionOptions _conv = convOpts.Value;

    public async Task<FloorMapResponse> GetAsync(Guid floorId)
    {
        var map = await db.FloorMaps.AsNoTracking().FirstOrDefaultAsync(m => m.FloorId == floorId);
        return map is null
            ? new FloorMapResponse(floorId, "None", null, null)
            : ToResponse(map);
    }

    public async Task<FloorMapResponse> UploadAndConvertAsync(Guid floorId, IFormFile file, CancellationToken ct)
    {
        _ = await db.Floors.FirstOrDefaultAsync(f => f.Id == floorId, ct)
            ?? throw new NotFoundException($"找不到樓層 {floorId}");

        if (!Path.GetExtension(file.FileName).Equals(".dxf", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("僅支援 .dxf 格式");
        if (file.Length == 0)
            throw new ValidationException("檔案內容不可為空");
        if (file.Length > _conv.MaxUploadBytes)
            throw new ValidationException($"檔案大小不可超過 {_conv.MaxUploadBytes / (1024 * 1024)}MB");

        // 1. 存原始 DXF
        var dxfDir = storage.DxfDir(floorId);
        Directory.CreateDirectory(dxfDir);
        var dxfPath = Path.Combine(dxfDir, $"{Guid.NewGuid()}.dxf");
        await using (var fs = File.Create(dxfPath))
            await file.CopyToAsync(fs, ct);

        // 2. 取得 / 建立 FloorMap，標記 Processing
        var map = await db.FloorMaps.FirstOrDefaultAsync(m => m.FloorId == floorId, ct);
        if (map is null)
        {
            map = new FloorMap { Id = Guid.NewGuid(), FloorId = floorId };
            db.FloorMaps.Add(map);
        }
        map.OriginalDxfPath = dxfPath;
        map.Status = FloorMapStatus.Processing;
        map.ErrorMessage = null;
        map.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // 3. 同步轉檔（暫存檔 → 原子搬移），含逾時保護
        var finalPath = storage.GeoJsonPath(floorId);
        var tmpPath = finalPath + ".tmp";
        try
        {
            Directory.CreateDirectory(storage.MapsDirAbsolute);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_conv.TimeoutSeconds));
            var token = timeoutCts.Token;

            var featureCount = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                return converter.Convert(dxfPath, tmpPath);
            }, token);

            if (featureCount == 0)
                throw new ValidationException("DXF 解析成功但未取得任何有效幾何，請確認模型空間含線條或文字");

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tmpPath, finalPath);

            map.GeoJsonPath = finalPath;
            map.Status = FloorMapStatus.Ready;
            map.ErrorMessage = null;
            map.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("樓層 {FloorId} 底圖轉檔完成", floorId);
            return ToResponse(map);
        }
        catch (Exception ex)
        {
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { /* 忽略暫存清理失敗 */ }
            }

            var friendly = ex switch
            {
                OperationCanceledException => "轉檔逾時，請確認 DXF 檔案大小與內容",
                ValidationException ve => ve.Message,
                _ => $"DXF 解析失敗：{ex.Message}"
            };

            map.Status = FloorMapStatus.Failed;
            map.ErrorMessage = friendly;
            map.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);

            logger.LogError(ex, "樓層 {FloorId} 底圖轉檔失敗", floorId);

            // 解析/轉檔失敗視為使用者輸入問題 → 400（呼應規格「解析失敗顯示錯誤並擋上傳」）
            throw new ValidationException(friendly);
        }
    }

    public async Task DeleteAsync(Guid floorId)
    {
        var map = await db.FloorMaps.FirstOrDefaultAsync(m => m.FloorId == floorId)
            ?? throw new NotFoundException($"找不到樓層 {floorId} 的底圖");

        db.FloorMaps.Remove(map);
        await db.SaveChangesAsync();
        await storage.DeleteFloorMapAsync(floorId);
    }

    private FloorMapResponse ToResponse(FloorMap map)
    {
        var geoJsonUrl = map.Status == FloorMapStatus.Ready
            ? $"{_mapOpts.PublicBasePath}/{map.FloorId}.geojson"
            : null;

        return new FloorMapResponse(
            map.FloorId,
            map.Status.ToString(),
            geoJsonUrl,
            map.ErrorMessage);
    }
}
