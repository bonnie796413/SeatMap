using BackEnd.Dtos.Seats;
using BackEnd.Services;

namespace BackEnd.Endpoints;

public static class SeatEndpoints
{
    public static IEndpointRouteBuilder MapSeatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/").WithTags("Seats");

        // GET /api/floors/{floorId}/seats
        group.MapGet("/floors/{floorId:guid}/seats", async (Guid floorId, SeatService svc) =>
            Results.Ok(await svc.GetByFloorAsync(floorId)))
            .RequireAuthorization();

        // GET /api/seats/{id}
        group.MapGet("/seats/{id:guid}", async (Guid id, SeatService svc) =>
            Results.Ok(await svc.GetByIdAsync(id)))
            .RequireAuthorization();

        // POST /api/seats
        group.MapPost("/seats", async (CreateSeatRequest req, SeatService svc) =>
        {
            var seat = await svc.CreateAsync(req);
            return Results.Created($"/api/seats/{seat.Id}", seat);
        }).RequireAuthorization("AdminOnly");

        // PUT /api/seats/{id}
        group.MapPut("/seats/{id:guid}", async (Guid id, UpdateSeatRequest req, SeatService svc) =>
            Results.Ok(await svc.UpdateAsync(id, req)))
            .RequireAuthorization("AdminOnly");

        // DELETE /api/seats/{id}
        group.MapDelete("/seats/{id:guid}", async (Guid id, SeatService svc) =>
        {
            await svc.DeleteAsync(id);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
