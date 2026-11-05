using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TicTacToe.Infrastructure;

public sealed class RoomCleanupService(OnlineRoomManager rooms, ILogger<RoomCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var removed = rooms.CleanupExpired(DateTimeOffset.UtcNow);
            if (removed > 0) logger.LogInformation("Removed {RoomCount} expired game rooms.", removed);
        }
    }
}
