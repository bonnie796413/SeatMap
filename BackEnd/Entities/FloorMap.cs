namespace BackEnd.Entities;

public enum FloorMapStatus
{
    Pending,
    Processing,
    Ready,
    Failed
}

public class FloorMap
{
    public Guid Id { get; set; }
    public Guid FloorId { get; set; }
    public string OriginalDxfPath { get; set; } = string.Empty;
    public string? TileDirectory { get; set; }
    public int MinZoom { get; set; }
    public int MaxZoom { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? BoundsJson { get; set; }
    public FloorMapStatus Status { get; set; } = FloorMapStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Floor Floor { get; set; } = null!;
}
