using System.Collections.Concurrent;
using System.Security.Cryptography;
using TicTacToe.Domain;

namespace TicTacToe.Infrastructure;

public sealed record RoomJoinResult(string Code, string ReconnectToken, PlayerSymbol Symbol, GameSnapshot Game, bool Ready);

public sealed class OnlineRoomManager
{
    private sealed class Room(string code, string xConnection, string xToken)
    {
        public string Code { get; } = code;
        public string? XConnection { get; set; } = xConnection;
        public string? OConnection { get; set; }
        public byte[] XTokenHash { get; } = Hash(xToken);
        public byte[]? OTokenHash { get; set; }
        public GameSession Game { get; } = GameSession.Create(GameMode.Online);
        public HashSet<PlayerSymbol> Rematches { get; } = [];
        public object Gate { get; } = new();
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;
    }
    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (string Code, PlayerSymbol Symbol)> _connections = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _disconnectTimers = new();
    private static readonly char[] CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public RoomJoinResult Create(string connectionId)
    {
        if (_connections.ContainsKey(connectionId)) throw new InvalidOperationException("This connection already belongs to a room.");
        var token = NewToken();
        while (true)
        {
            var code = string.Concat(Enumerable.Range(0, 6).Select(_ => CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]));
            var room = new Room(code, connectionId, token);
            if (!_rooms.TryAdd(code, room)) continue;
            _connections[connectionId] = (code, PlayerSymbol.X);
            return new(code, token, PlayerSymbol.X, room.Game.Snapshot(), false);
        }
    }
    public GameSnapshot? Snapshot(string code)
    {
        if (!_rooms.TryGetValue(code, out var room)) return null;
        lock (room.Gate) return room.Game.Snapshot();
    }
    public RoomJoinResult? Join(string code, string connectionId)
    {
        if (_connections.ContainsKey(connectionId)) return null;
        if (!_rooms.TryGetValue(code, out var room)) return null;
        lock (room.Gate)
        {
            if (room.OTokenHash is not null || room.Game.MoveCount > 0 || room.Game.Status == GameStatus.Finished) return null;
            var token = NewToken(); room.OConnection = connectionId; room.OTokenHash = Hash(token); _connections[connectionId] = (room.Code, PlayerSymbol.O);
            room.LastActivity = DateTimeOffset.UtcNow;
            return new(room.Code, token, PlayerSymbol.O, room.Game.Snapshot(), true);
        }
    }
    public (GameSnapshot? Game, string? Error) Move(string code, string connectionId, int cell)
    {
        if (!_rooms.TryGetValue(code, out var room)) return (null, "Room not found.");
        lock (room.Gate)
        {
            if (!_connections.TryGetValue(connectionId, out var membership) || !membership.Code.Equals(room.Code, StringComparison.OrdinalIgnoreCase)) return (null, "You are not a player in this room.");
            if (room.OConnection is null) return (null, "Waiting for another player.");
            if (room.Game.CurrentTurn != membership.Symbol) return (null, "It is not your turn.");
            if (!room.Game.TryMove(cell)) return (null, "That move is unavailable.");
            room.LastActivity = DateTimeOffset.UtcNow;
            return (room.Game.Snapshot(), null);
        }
    }
    public (string? Connection, PlayerSymbol? Symbol, GameSnapshot? Game, bool Ready) Reconnect(string code, string token, string connectionId)
    {
        if (!_rooms.TryGetValue(code, out var room)) return (null, null, null, false);
        if (_connections.ContainsKey(connectionId)) return (null, null, null, false);
        lock (room.Gate)
        {
            var hash=Hash(token);
            var symbol = CryptographicOperations.FixedTimeEquals(hash, room.XTokenHash) ? PlayerSymbol.X : room.OTokenHash is not null && CryptographicOperations.FixedTimeEquals(hash, room.OTokenHash) ? PlayerSymbol.O : (PlayerSymbol?)null;
            if (symbol is null) return (null, null, null, false);
            var previous = symbol == PlayerSymbol.X ? room.XConnection : room.OConnection;
            if (previous is not null) return (null, null, null, false);
            if (symbol == PlayerSymbol.X) room.XConnection = connectionId; else room.OConnection = connectionId;
            room.LastActivity = DateTimeOffset.UtcNow;
            _connections[connectionId] = (room.Code, symbol.Value);
            if (_disconnectTimers.TryRemove(room.Code + symbol, out var timer)) { timer.Cancel(); timer.Dispose(); }
            return (previous, symbol, room.Game.Snapshot(), room.XConnection is not null && room.OConnection is not null);
        }
    }
    public (string Code, PlayerSymbol Symbol)? Disconnect(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var member) || !_rooms.TryGetValue(member.Code, out var room)) return null;
        lock (room.Gate)
        {
            if (member.Symbol == PlayerSymbol.X && room.XConnection == connectionId) room.XConnection = null;
            if (member.Symbol == PlayerSymbol.O && room.OConnection == connectionId) room.OConnection = null;
        }
        return member;
    }
    public bool ForfeitIfDisconnected(string code, PlayerSymbol symbol)
    {
        if (!_rooms.TryGetValue(code, out var room)) return false;
        lock (room.Gate)
        {
            var disconnected = symbol == PlayerSymbol.X ? room.XConnection is null : room.OConnection is null;
            if (symbol == PlayerSymbol.X && room.OConnection is null && disconnected)
            {
                _rooms.TryRemove(room.Code, out _);
                return false;
            }
            var opponentConnected = symbol == PlayerSymbol.X ? room.OConnection is not null : room.XConnection is not null;
            if (!disconnected || !opponentConnected || room.Game.Status != GameStatus.InProgress) return false;
            room.Game.Forfeit(symbol == PlayerSymbol.X ? PlayerSymbol.O : PlayerSymbol.X); return true;
        }
    }
    public bool RequestRematch(string code, string connectionId, out GameSnapshot? game)
    {
        game = null;
        if (!_rooms.TryGetValue(code, out var room)) return false;
        lock (room.Gate)
        {
            if (!_connections.TryGetValue(connectionId, out var member) || !member.Code.Equals(room.Code, StringComparison.OrdinalIgnoreCase) || room.Game.Status != GameStatus.Finished) return false;
            room.Rematches.Add(member.Symbol);
            if (room.Rematches.Count == 2) { room.Game.Reset(); room.Rematches.Clear(); }
            room.LastActivity = DateTimeOffset.UtcNow;
            game = room.Game.Snapshot(); return true;
        }
    }
    public int CleanupExpired(DateTimeOffset now)
    {
        var removed = 0;
        foreach (var pair in _rooms.ToArray())
        {
            var room = pair.Value;
            lock (room.Gate)
            {
                var waitingExpired = room.OConnection is null && now - room.CreatedAt >= TimeSpan.FromMinutes(15);
                var finishedExpired = room.Game.Status == GameStatus.Finished && now - room.LastActivity >= TimeSpan.FromHours(1);
                if (!waitingExpired && !finishedExpired) continue;
                if (!((ICollection<KeyValuePair<string, Room>>)_rooms).Remove(pair)) continue;
                if (room.XConnection is { } x) _connections.TryRemove(x, out _);
                if (room.OConnection is { } o) _connections.TryRemove(o, out _);
                removed++;
            }
        }
        return removed;
    }
    public CancellationTokenSource StartDisconnectTimer(string code, PlayerSymbol symbol)
    {
        var key = code + symbol; var cts = new CancellationTokenSource();
        if (_disconnectTimers.TryRemove(key, out var old)) { old.Cancel(); old.Dispose(); }
        _disconnectTimers[key] = cts;
        return cts;
    }
    public void RemoveDisconnectTimer(string code, PlayerSymbol symbol, CancellationTokenSource? expected = null)
    {
        var key = code + symbol;
        if (!_disconnectTimers.TryGetValue(key, out var timer) || expected is not null && !ReferenceEquals(timer, expected)) return;
        if (((ICollection<KeyValuePair<string, CancellationTokenSource>>)_disconnectTimers).Remove(new(key, timer)))
        {
            timer.Cancel(); timer.Dispose();
        }
    }
    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static byte[] Hash(string token) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
}
