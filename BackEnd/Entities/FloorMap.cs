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

    /// <summary>原始 DXF 儲存路徑。</summary>
    public string OriginalDxfPath { get; set; } = string.Empty;

    /// <summary>轉檔輸出的 GeoJSON 檔案路徑（成功後才有值）。</summary>
    public string? GeoJsonPath { get; set; }

    public FloorMapStatus Status { get; set; } = FloorMapStatus.Pending;

    /// <summary>轉檔失敗訊息。</summary>
    public string? ErrorMessage { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Floor Floor { get; set; } = null!;
}
