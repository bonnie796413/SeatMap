using NetTopologySuite.Geometries;

namespace BackEnd.Entities;

public class Seat
{
    public Guid Id { get; set; }
    public Guid FloorId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public Point Location { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public Floor Floor { get; set; } = null!;
    public SeatAssignment? SeatAssignment { get; set; }
}
