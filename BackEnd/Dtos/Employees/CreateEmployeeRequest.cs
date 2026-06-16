namespace BackEnd.Dtos.Employees;

public record CreateEmployeeRequest(
    string FullName,
    string? Department,
    string? AvatarUrl,
    string Username,
    string Password);
