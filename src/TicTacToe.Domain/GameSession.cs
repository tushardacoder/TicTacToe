namespace TicTacToe.Domain;

public enum PlayerSymbol { X, O }
public enum GameMode { Local, VsAi, Online }
public enum GameStatus { WaitingForPlayers, InProgress, Finished }
public enum GameResult { PlayerXWins, PlayerOWins, Draw, PlayerXWinsByForfeit, PlayerOWinsByForfeit }
public sealed record GameSnapshot(Guid Id, GameMode Mode, IReadOnlyList<PlayerSymbol?> Cells, PlayerSymbol CurrentTurn, GameStatus Status, GameResult? Result, int MoveCount, IReadOnlyList<int> WinningLine);

public sealed class GameSession
{
    private static readonly int[][] Wins = [[0,1,2],[3,4,5],[6,7,8],[0,3,6],[1,4,7],[2,5,8],[0,4,8],[2,4,6]];
    private readonly PlayerSymbol?[] _cells = new PlayerSymbol?[9];
    private readonly List<int> _winningLine = [];
    private GameSession(GameMode mode) { Id = Guid.NewGuid(); Mode = mode; }
    public Guid Id { get; }
    public GameMode Mode { get; }
    public IReadOnlyList<PlayerSymbol?> Cells => Array.AsReadOnly((PlayerSymbol?[])_cells.Clone());
    public PlayerSymbol CurrentTurn { get; private set; } = PlayerSymbol.X;
    public GameStatus Status { get; private set; } = GameStatus.InProgress;
    public GameResult? Result { get; private set; }
    public int MoveCount { get; private set; }
    public IReadOnlyList<int> WinningLine => _winningLine.AsReadOnly();
    public static GameSession Create(GameMode mode) => new(mode);
    public GameSnapshot Snapshot() => new(Id, Mode, Cells, CurrentTurn, Status, Result, MoveCount, WinningLine.ToArray());
    public void Forfeit(PlayerSymbol winner)
    {
        if (Status != GameStatus.InProgress) return;
        Result = winner == PlayerSymbol.X ? GameResult.PlayerXWinsByForfeit : GameResult.PlayerOWinsByForfeit;
        Status = GameStatus.Finished;
    }
    public void Reset()
    {
        Array.Clear(_cells); _winningLine.Clear(); MoveCount = 0; CurrentTurn = PlayerSymbol.X; Result = null; Status = GameStatus.InProgress;
    }
    public bool TryMove(int index)
    {
        if (Status != GameStatus.InProgress || index is < 0 or > 8 || _cells[index] is not null) return false;
        _cells[index] = CurrentTurn; MoveCount++;
        var line = Wins.FirstOrDefault(w => w.All(i => _cells[i] == CurrentTurn));
        if (line is not null) { _winningLine.AddRange(line); Result = CurrentTurn == PlayerSymbol.X ? GameResult.PlayerXWins : GameResult.PlayerOWins; Status = GameStatus.Finished; }
        else if (MoveCount == 9) { Result = GameResult.Draw; Status = GameStatus.Finished; }
        else CurrentTurn = CurrentTurn == PlayerSymbol.X ? PlayerSymbol.O : PlayerSymbol.X;
        return true;
    }
}
