namespace BackEnd.Dtos.Seats;

public record UpdateSeatRequest(
    string SeatNumber,
    double X,
    double Y);
