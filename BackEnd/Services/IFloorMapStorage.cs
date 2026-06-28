namespace BackEnd.Services;

/// <summary>底圖檔案（GeoJSON 與原始 DXF）的清理介面，供樓層刪除時呼叫。</summary>
public interface IFloorMapStorage
{
    Task DeleteFloorMapAsync(Guid floorId);
}
