using BackEnd.Services;

namespace BackEnd.Endpoints;

public static class FloorMapEndpoints
{
    public static IEndpointRouteBuilder MapFloorMapEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/floors").WithTags("FloorMap");

        // GET /api/floors/{floorId}/map — 取得底圖中繼資料（供頁面載入）
        group.MapGet("/{floorId:guid}/map", async (Guid floorId, FloorMapService svc) =>
            Results.Ok(await svc.GetAsync(floorId)))
            .RequireAuthorization();

        // POST /api/floors/{floorId}/map — 上傳 DXF，同步轉檔為 GeoJSON
        group.MapPost("/{floorId:guid}/map", async (
            Guid floorId,
            IFormFile file,
            FloorMapService svc,
            CancellationToken ct) =>
        {
            var result = await svc.UploadAndConvertAsync(floorId, file, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization("AdminOnly")
        .DisableAntiforgery();

        // DELETE /api/floors/{floorId}/map — 移除底圖（保留樓層）
        group.MapDelete("/{floorId:guid}/map", async (Guid floorId, FloorMapService svc) =>
        {
            await svc.DeleteAsync(floorId);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
