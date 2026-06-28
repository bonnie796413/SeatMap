namespace BackEnd.Options;

public class MapStorageOptions
{
    /// <summary>底圖檔案根目錄（本機 ./_data、容器 /data）。</summary>
    public string RootPath { get; set; } = "./_data";

    /// <summary>GeoJSON 對外靜態路由前綴。</summary>
    public string PublicBasePath { get; set; } = "/maps";
}
