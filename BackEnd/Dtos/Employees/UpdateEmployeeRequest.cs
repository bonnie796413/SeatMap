namespace BackEnd.Dtos.Employees;

public record UpdateEmployeeRequest(
    string FullName,
    string? Department,
    string? AvatarUrl);
