namespace BackEnd.Dtos.Seats;

public record AssignmentInfo(
    Guid EmployeeId,
    string FullName,
    string? AvatarUrl,
    string? Department);
