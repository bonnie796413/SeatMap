namespace BackEnd.Dtos.Seats;

public record CreateSeatRequest(
    Guid FloorId,
    string SeatNumber,
    double X,
    double Y);
