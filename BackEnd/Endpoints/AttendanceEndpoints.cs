using System.Security.Claims;
using BackEnd.Services;

namespace BackEnd.Endpoints;

public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/attendance").WithTags("Attendance");

        group.MapPost("/check-in", async (ClaimsPrincipal principal, AttendanceService svc) =>
        {
            var employeeId = GetEmployeeId(principal);
            return Results.Ok(await svc.CheckInAsync(employeeId));
        }).RequireAuthorization();

        group.MapPost("/check-out", async (ClaimsPrincipal principal, AttendanceService svc) =>
        {
            var employeeId = GetEmployeeId(principal);
            return Results.Ok(await svc.CheckOutAsync(employeeId));
        }).RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal principal, AttendanceService svc) =>
        {
            var employeeId = GetEmployeeId(principal);
            return Results.Ok(await svc.GetStatusAsync(employeeId));
        }).RequireAuthorization();

        return app;
    }

    private static Guid GetEmployeeId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("employeeId")?.Value;
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var id))
            throw new BackEnd.Infrastructure.ValidationException("此帳號無對應員工，無法打卡");
        return id;
    }
}
