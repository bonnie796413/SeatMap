namespace BackEnd.Dtos.Employees;

public record EmployeeSearchResult(
    Guid EmployeeId,
    string FullName,
    string? Department,
    string? AvatarUrl,
    bool IsPresent,
    SeatSummary? Seat);
