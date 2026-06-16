namespace BackEnd.Dtos.Employees;

public record SeatSummary(
    Guid SeatId,
    Guid FloorId,
    string SeatNumber,
    double X,
    double Y);
