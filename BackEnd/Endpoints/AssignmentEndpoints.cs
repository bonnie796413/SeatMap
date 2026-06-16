using BackEnd.Dtos.Assignments;
using BackEnd.Services;

namespace BackEnd.Endpoints;

public static class AssignmentEndpoints
{
    public static IEndpointRouteBuilder MapAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var assignGroup = app.MapGroup("/assignments").WithTags("Assignments");
        var seatsGroup = app.MapGroup("/seats");
        var empGroup = app.MapGroup("/employees");

        assignGroup.MapPost("/", async (AssignSeatRequest req, AssignmentService svc) =>
        {
            var result = await svc.AssignAsync(req.SeatId, req.EmployeeId);
            return Results.Created("/api/assignments", result);
        }).RequireAuthorization("AdminOnly");

        seatsGroup.MapDelete("/{seatId:guid}/assignment", async (Guid seatId, AssignmentService svc) =>
        {
            await svc.UnassignBySeatAsync(seatId);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        empGroup.MapDelete("/{employeeId:guid}/assignment", async (Guid employeeId, AssignmentService svc) =>
        {
            await svc.UnassignByEmployeeAsync(employeeId);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
