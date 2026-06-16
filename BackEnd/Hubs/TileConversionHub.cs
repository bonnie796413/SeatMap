using Microsoft.AspNetCore.SignalR;

namespace BackEnd.Hubs;

public class TileConversionHub : Hub
{
    public async Task JoinFloorGroup(string floorId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"floor-{floorId}");

    public async Task LeaveFloorGroup(string floorId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"floor-{floorId}");
}
