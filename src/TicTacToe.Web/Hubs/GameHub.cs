using Microsoft.AspNetCore.SignalR;
using TicTacToe.Domain;
using TicTacToe.Infrastructure;

namespace TicTacToe.Web.Hubs;

public sealed class GameHub(OnlineRoomManager rooms, IHubContext<GameHub> hubContext, ILogger<GameHub> logger) : Hub
{
    public async Task<RoomActionResponse> CreateRoom()
    {
        try
        {
            var result = rooms.Create(Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, result.Code);
            logger.LogInformation("Created online room {RoomCode}.", result.Code);
            return new(true, null, result.Symbol, result.Game, result.Ready, result.Code, result.ReconnectToken);
        }
        catch (InvalidOperationException)
        {
            logger.LogInformation("Rejected a second room assignment for connection {ConnectionId}.", Context.ConnectionId);
            return new(false, "This connection is already in a room.");
        }
    }
    public async Task<RoomActionResponse> JoinRoom(string code)
    {
        var result = rooms.Join(code.Trim(), Context.ConnectionId);
        if (result is null)
        {
            logger.LogInformation("Rejected a room join for connection {ConnectionId}.", Context.ConnectionId);
            return new(false, "Room code is invalid, the room is full, or the match has started.");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, result.Code);
        await Clients.Group(result.Code).SendAsync("RoomReady", result.Game);
        logger.LogInformation("Player joined online room {RoomCode} as {PlayerSymbol}.", result.Code, result.Symbol);
        return new(true, null, result.Symbol, result.Game, result.Ready, result.Code, result.ReconnectToken);
    }
    public async Task<RoomActionResponse> Reconnect(string code, string token)
    {
        var result=rooms.Reconnect(code, token, Context.ConnectionId);
        if (result.Symbol is null || result.Game is null)
            return new(false, "Reconnect credentials are invalid or expired.");
        var room = code.ToUpperInvariant();
        await Groups.AddToGroupAsync(Context.ConnectionId, room);
        await Clients.Group(room).SendAsync("PlayerReconnected", new { result.Symbol, result.Game, result.Ready });
        logger.LogInformation("Player reconnected to online room {RoomCode} as {PlayerSymbol}.", room, result.Symbol);
        return new(true, null, result.Symbol, result.Game, result.Ready);
    }
    public async Task<RoomActionResponse> MakeMove(string code, int cellIndex)
    {
        var room = code.Trim().ToUpperInvariant();
        var result=rooms.Move(room, Context.ConnectionId, cellIndex);
        if (result.Game is null)
        {
            logger.LogInformation("Rejected online move for room {RoomCode}: {Reason}.", room, result.Error);
            return new(false, result.Error ?? "Move rejected.");
        }
        logger.LogInformation("Accepted online move for room {RoomCode} at cell {CellIndex}.", room, cellIndex);
        await Clients.Group(room).SendAsync("GameUpdated", result.Game);
        return new(true, null, Game: result.Game);
    }
    public async Task<RoomActionResponse> RequestRematch(string code)
    {
        var room = code.Trim().ToUpperInvariant();
        if (!rooms.RequestRematch(room, Context.ConnectionId, out var game, out var requestedBy) || game is null)
            return new(false, "Rematch request rejected.");
        await Clients.Group(room).SendAsync("RematchStatus", new { requestedBy, game });
        if (game.MoveCount == 0 && game.Status == TicTacToe.Domain.GameStatus.InProgress)
            await Clients.Group(room).SendAsync("GameUpdated", game);
        return new(true, null, Game: game);
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var member=rooms.Disconnect(Context.ConnectionId);
        if (member is not null)
        {
            var (code, symbol)=member.Value;
            logger.LogInformation("Player disconnected from online room {RoomCode} as {PlayerSymbol}.", code, symbol);
            await Clients.Group(code).SendAsync("PlayerDisconnected", symbol);
            var timer=rooms.StartDisconnectTimer(code, symbol);
            _=Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), timer.Token);
                    if (rooms.ForfeitIfDisconnected(code, symbol) && rooms.Snapshot(code) is { } finalGame)
                        await hubContext.Clients.Group(code).SendAsync("GameUpdated", finalGame);
                }
                catch (OperationCanceledException) { }
                finally { rooms.RemoveDisconnectTimer(code, symbol, timer); }
            });
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public sealed record RoomActionResponse(bool Success, string? Error = null, PlayerSymbol? Symbol = null, GameSnapshot? Game = null, bool Ready = false, string? Code = null, string? ReconnectToken = null);
