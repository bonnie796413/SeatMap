namespace BackEnd.Dtos.Attendance;

public record AttendanceStatusResponse(
    Guid EmployeeId,
    bool IsPresent,
    DateTime? LastCheckInAt,
    DateTime? LastCheckOutAt,
    DateTime UpdatedAt);
