using System.Collections.Concurrent;
using TicTacToe.Application;
using TicTacToe.Domain;

namespace TicTacToe.Infrastructure;

public sealed class InMemoryGameStore : IGameStore
{
    private readonly ConcurrentDictionary<Guid, GameSession> _games = new();
    private readonly ConcurrentDictionary<Guid, object> _locks = new();
    public GameSession? Get(Guid id) => _games.GetValueOrDefault(id);
    public GameSnapshot? Snapshot(Guid id)
    {
        if (!_games.TryGetValue(id, out var game) || !_locks.TryGetValue(id, out var gate)) return null;
        lock (gate) return game.Snapshot();
    }
    public GameSession Create(GameMode mode) { var game=GameSession.Create(mode); _games[game.Id]=game; _locks[game.Id]=new object(); return game; }
    public bool Move(Guid id, int cell, PlayerSymbol? expectedTurn = null)
    {
        if (!_games.TryGetValue(id, out var game) || !_locks.TryGetValue(id, out var gate)) return false;
        lock (gate)
        {
            if (expectedTurn is not null && game.CurrentTurn != expectedTurn) return false;
            return game.TryMove(cell);
        }
    }
}
