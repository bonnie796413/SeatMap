namespace BackEnd.Dtos.Employees;

public record EmployeeResponse(
    Guid Id,
    string FullName,
    string? Department,
    string? AvatarUrl,
    string? Username,
    bool IsPresent,
    SeatSummary? Seat);
