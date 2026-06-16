namespace BackEnd.Services;

public class TileConversionWorker(
    TileConversionChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<TileConversionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var floorId in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<TileConversionService>();
                await svc.ConvertAsync(floorId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TileConversionWorker 處理 {FloorId} 時發生例外", floorId);
            }
        }
    }
}
