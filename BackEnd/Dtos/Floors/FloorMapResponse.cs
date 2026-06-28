namespace BackEnd.Dtos.Floors;

public record FloorMapResponse(
    Guid FloorId,
    string Status,
    string? GeoJsonUrl,
    string? ErrorMessage);
