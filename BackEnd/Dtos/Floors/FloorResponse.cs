namespace BackEnd.Dtos.Floors;

public record FloorResponse(
    Guid Id,
    string Name,
    int DisplayOrder,
    int SeatCount,
    string MapStatus);
