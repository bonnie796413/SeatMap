using System.Security.Claims;
using BackEnd.Dtos.Auth;
using BackEnd.Entities;
using Microsoft.AspNetCore.Identity;

namespace BackEnd.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", async (
            LoginRequest req,
            SignInManager<ApplicationUser> signInManager) =>
        {
            signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
            var result = await signInManager.PasswordSignInAsync(
                req.Username, req.Password, isPersistent: false, lockoutOnFailure: false);
            if (!result.Succeeded)
                return Results.Problem("帳號或密碼錯誤", statusCode: StatusCodes.Status401Unauthorized);
            return Results.Empty;
        }).AllowAnonymous();

        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            return Results.Ok(new MeResponse(
                user.Id,
                user.UserName,
                roles.FirstOrDefault(),
                user.EmployeeId));
        }).RequireAuthorization();

        return group;
    }
}
