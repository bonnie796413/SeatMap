namespace BackEnd.Dtos.Seats;

public record SeatResponse(
    Guid Id,
    Guid FloorId,
    string SeatNumber,
    double X,
    double Y,
    AssignmentInfo? Assignment,
    bool? IsPresent);
