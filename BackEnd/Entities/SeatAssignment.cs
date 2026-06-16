namespace BackEnd.Entities;

public class SeatAssignment
{
    public Guid Id { get; set; }
    public Guid SeatId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime AssignedAt { get; set; }

    public Seat Seat { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
