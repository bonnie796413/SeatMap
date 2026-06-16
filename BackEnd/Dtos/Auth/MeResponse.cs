namespace BackEnd.Dtos.Auth;

public record MeResponse(
    string UserId,
    string? Username,
    string? Role,
    Guid? EmployeeId);
