using BackEnd.Data;
using BackEnd.Dtos.Floors;
using BackEnd.Entities;
using BackEnd.Infrastructure;
using BackEnd.Options;
using BackEnd.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackEnd.Endpoints;

public static class FloorMapEndpoints
{
    public static IEndpointRouteBuilder MapFloorMapEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/floors").WithTags("FloorMap");

        // GET /api/floors/{floorId}/map
        group.MapGet("/{floorId:guid}/map", async (
            Guid floorId,
            AppDbContext db,
            IOptions<TilesOptions> tilesOpts) =>
        {
            var map = await db.FloorMaps.FirstOrDefaultAsync(m => m.FloorId == floorId);
            if (map is null)
                return Results.Ok(new FloorMapResponse(floorId, "None", 0, 4, null, null, null));

            var tileUrlTemplate = map.Status == FloorMapStatus.Ready
                ? $"{tilesOpts.Value.PublicBasePath}/{floorId}/{{z}}/{{x}}/{{y}}.png"
                : null;

            return Results.Ok(new FloorMapResponse(
                floorId,
                map.Status.ToString(),
                map.MinZoom,
                map.MaxZoom,
                map.BoundsJson,
                tileUrlTemplate,
                map.ErrorMessage));
        }).RequireAuthorization();

        // POST /api/floors/{floorId}/map  (上傳 DXF)
        group.MapPost("/{floorId:guid}/map", async (
            Guid floorId,
            IFormFile file,
            AppDbContext db,
            TileConversionChannel conversionChannel,
            IOptions<TilesOptions> tilesOpts,
            ILogger<Program> logger) =>
        {
            // 驗證樓層存在
            var floor = await db.Floors.FirstOrDefaultAsync(f => f.Id == floorId)
                ?? throw new NotFoundException($"找不到樓層 {floorId}");

            // 驗證副檔名
            if (!Path.GetExtension(file.FileName).Equals(".dxf", StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("僅支援 .dxf 格式");

            if (file.Length == 0)
                throw new ValidationException("檔案內容不可為空");

            if (file.Length > 50 * 1024 * 1024)
                throw new ValidationException("檔案大小不可超過 50MB");

            // 若正在處理中，拒絕重複上傳
            var existingMap = await db.FloorMaps.FirstOrDefaultAsync(m => m.FloorId == floorId);
            if (existingMap?.Status == FloorMapStatus.Processing)
                throw new ConflictException("底圖正在轉檔中，請稍後再試");

            // 儲存 DXF 原始檔
            var dxfRoot = Path.GetFullPath(Path.Combine(
                tilesOpts.Value.RootPath, "..", "dxf", floorId.ToString()));
            Directory.CreateDirectory(dxfRoot);
            var dxfFileName = $"{Guid.NewGuid()}.dxf";
            var dxfPath = Path.Combine(dxfRoot, dxfFileName);

            using (var stream = File.Create(dxfPath))
                await file.CopyToAsync(stream);

            // 建立或更新 FloorMap
            if (existingMap is null)
            {
                existingMap = new FloorMap
                {
                    Id = Guid.NewGuid(),
                    FloorId = floorId,
                    OriginalDxfPath = dxfPath,
                    Status = FloorMapStatus.Pending,
                    MinZoom = 0,
                    MaxZoom = 4,
                    UpdatedAt = DateTime.UtcNow
                };
                db.FloorMaps.Add(existingMap);
            }
            else
            {
                existingMap.OriginalDxfPath = dxfPath;
                existingMap.Status = FloorMapStatus.Pending;
                existingMap.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();

            // 推送背景轉檔任務
            await conversionChannel.Writer.WriteAsync(floorId);

            return Results.Accepted($"/api/floors/{floorId}/map",
                new { status = "Processing", floorId });
        })
        .RequireAuthorization("AdminOnly")
        .DisableAntiforgery();

        // DELETE /api/floors/{floorId}/map
        group.MapDelete("/{floorId:guid}/map", async (
            Guid floorId,
            AppDbContext db,
            ITileStorage tileStorage) =>
        {
            var map = await db.FloorMaps.FirstOrDefaultAsync(m => m.FloorId == floorId)
                ?? throw new NotFoundException($"找不到樓層 {floorId} 的底圖");

            db.FloorMaps.Remove(map);
            await db.SaveChangesAsync();
            await tileStorage.DeleteFloorTilesAsync(floorId);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
