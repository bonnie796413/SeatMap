using BackEnd.Dtos.Employees;
using BackEnd.Services;

namespace BackEnd.Endpoints;

public static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/employees").WithTags("Employees");

        group.MapGet("/", async (EmployeeService svc) =>
            Results.Ok(await svc.GetAllAsync()))
            .RequireAuthorization("AdminOnly");

        group.MapGet("/search", async (string name, EmployeeService svc) =>
            Results.Ok(await svc.SearchAsync(name)))
            .RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, EmployeeService svc) =>
            Results.Ok(await svc.GetByIdAsync(id)))
            .RequireAuthorization();

        group.MapPost("/", async (CreateEmployeeRequest req, EmployeeService svc) =>
        {
            var emp = await svc.CreateAsync(req);
            return Results.Created($"/api/employees/{emp.Id}", emp);
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/{id:guid}", async (Guid id, UpdateEmployeeRequest req, EmployeeService svc) =>
            Results.Ok(await svc.UpdateAsync(id, req)))
            .RequireAuthorization("AdminOnly");

        group.MapDelete("/{id:guid}", async (Guid id, EmployeeService svc) =>
        {
            await svc.DeleteAsync(id);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
