using Microsoft.Extensions.DependencyInjection;
using TicTacToe.Application;
using TicTacToe.Domain;

namespace TicTacToe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        services.AddSingleton<MinimaxAi>();
        services.AddSingleton<IGameStore, InMemoryGameStore>();
        services.AddSingleton<OnlineRoomManager>();
        services.AddHostedService<RoomCleanupService>();
        services.AddSingleton<GameService>();
        return services;
    }
}
