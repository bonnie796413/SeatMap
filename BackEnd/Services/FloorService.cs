using BackEnd.Data;
using BackEnd.Dtos.Floors;
using BackEnd.Entities;
using BackEnd.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BackEnd.Services;

public class FloorService(AppDbContext db, ITileStorage tileStorage, ILogger<FloorService> logger)
{
    public async Task<List<FloorResponse>> GetAllAsync()
    {
        return await db.Floors
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new FloorResponse(
                f.Id,
                f.Name,
                f.DisplayOrder,
                f.Seats.Count,
                f.FloorMap == null ? "None" : f.FloorMap.Status.ToString()))
            .ToListAsync();
    }

    public async Task<FloorResponse> GetByIdAsync(Guid id)
    {
        var floor = await db.Floors
            .Include(f => f.FloorMap)
            .Include(f => f.Seats)
            .FirstOrDefaultAsync(f => f.Id == id)
            ?? throw new NotFoundException($"找不到樓層 {id}");

        return ToResponse(floor);
    }

    public async Task<FloorResponse> CreateAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("樓層名稱不可為空");

        var maxOrder = await db.Floors.MaxAsync(f => (int?)f.DisplayOrder) ?? 0;
        var floor = new Floor
        {
            Id = Guid.NewGuid(),
            Name = name,
            DisplayOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        };
        db.Floors.Add(floor);
        await db.SaveChangesAsync();
        return ToResponse(floor);
    }

    public async Task<FloorResponse> RenameAsync(Guid id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("樓層名稱不可為空");

        var floor = await db.Floors
            .Include(f => f.Seats)
            .Include(f => f.FloorMap)
            .FirstOrDefaultAsync(f => f.Id == id)
            ?? throw new NotFoundException($"找不到樓層 {id}");

        floor.Name = name;
        await db.SaveChangesAsync();
        return ToResponse(floor);
    }

    public async Task<List<FloorResponse>> ReorderAsync(Guid[] orderedFloorIds)
    {
        var floors = await db.Floors
            .Include(f => f.Seats)
            .Include(f => f.FloorMap)
            .ToListAsync();

        if (floors.Select(f => f.Id).OrderBy(x => x).SequenceEqual(orderedFloorIds.OrderBy(x => x)) == false)
            throw new ValidationException("傳入的樓層 ID 集合與現有樓層不符");

        for (int i = 0; i < orderedFloorIds.Length; i++)
        {
            var floor = floors.First(f => f.Id == orderedFloorIds[i]);
            floor.DisplayOrder = i + 1;
        }
        await db.SaveChangesAsync();

        return floors.OrderBy(f => f.DisplayOrder).Select(ToResponse).ToList();
    }

    public async Task DeleteAsync(Guid id)
    {
        var floor = await db.Floors
            .Include(f => f.FloorMap)
            .FirstOrDefaultAsync(f => f.Id == id)
            ?? throw new NotFoundException($"找不到樓層 {id}");

        db.Floors.Remove(floor);
        await db.SaveChangesAsync();

        try
        {
            await tileStorage.DeleteFloorTilesAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "刪除樓層 {FloorId} 的 Tile 檔案失敗，請手動清理", id);
        }
    }

    private static FloorResponse ToResponse(Floor floor) =>
        new(floor.Id, floor.Name, floor.DisplayOrder,
            floor.Seats.Count,
            floor.FloorMap?.Status.ToString() ?? "None");
}
