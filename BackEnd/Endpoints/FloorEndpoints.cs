using BackEnd.Dtos.Floors;
using BackEnd.Services;

namespace BackEnd.Endpoints;

public static class FloorEndpoints
{
    public static IEndpointRouteBuilder MapFloorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/floors").WithTags("Floors");

        group.MapGet("/", async (FloorService svc) =>
            Results.Ok(await svc.GetAllAsync()))
            .RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, FloorService svc) =>
            Results.Ok(await svc.GetByIdAsync(id)))
            .RequireAuthorization();

        group.MapPost("/", async (CreateFloorRequest req, FloorService svc) =>
        {
            var floor = await svc.CreateAsync(req.Name);
            return Results.Created($"/api/floors/{floor.Id}", floor);
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/reorder", async (ReorderFloorsRequest req, FloorService svc) =>
            Results.Ok(await svc.ReorderAsync(req.OrderedFloorIds)))
            .RequireAuthorization("AdminOnly");

        group.MapPut("/{id:guid}", async (Guid id, UpdateFloorRequest req, FloorService svc) =>
            Results.Ok(await svc.RenameAsync(id, req.Name)))
            .RequireAuthorization("AdminOnly");

        group.MapDelete("/{id:guid}", async (Guid id, FloorService svc) =>
        {
            await svc.DeleteAsync(id);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
