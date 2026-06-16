namespace BackEnd.Dtos.Floors;

public record FloorMapResponse(
    Guid FloorId,
    string Status,
    int MinZoom,
    int MaxZoom,
    string? BoundsJson,
    string? TileUrlTemplate,
    string? ErrorMessage);
