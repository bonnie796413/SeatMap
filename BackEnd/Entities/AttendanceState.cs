namespace BackEnd.Entities;

public class AttendanceState
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public bool IsPresent { get; set; }
    public DateTime? LastCheckInAt { get; set; }
    public DateTime? LastCheckOutAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Employee Employee { get; set; } = null!;
}
