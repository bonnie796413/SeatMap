namespace BackEnd.Entities;

public class Employee
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public SeatAssignment? SeatAssignment { get; set; }
    public AttendanceState? AttendanceState { get; set; }
}
