namespace BackEnd.Options;

public class GeoJsonConversionOptions
{
    /// <summary>DXF 上傳大小上限（bytes），預設 50MB。</summary>
    public long MaxUploadBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>單次轉檔逾時保護（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 120;
}
