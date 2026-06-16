namespace BackEnd.Services;

public interface ITileStorage
{
    Task DeleteFloorTilesAsync(Guid floorId);
}
