namespace BackEnd.Dtos.Assignments;

public record AssignmentResponse(
    Guid SeatId,
    string SeatNumber,
    Guid FloorId,
    Guid EmployeeId,
    string FullName,
    DateTime AssignedAt);
