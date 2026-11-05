using TicTacToe.Domain;

namespace TicTacToe.Application;

public interface IGameStore
{
    GameSession? Get(Guid id);
    GameSnapshot? Snapshot(Guid id);
    GameSession Create(GameMode mode);
    bool Move(Guid id, int cell, PlayerSymbol? expectedTurn = null);
}

public sealed class GameService(IGameStore store, MinimaxAi ai)
{
    public GameSnapshot Create(GameMode mode) { var game=store.Create(mode); return store.Snapshot(game.Id)!; }
    public GameSnapshot? Get(Guid id) => store.Snapshot(id);
    public bool Move(Guid id, int cell)
    {
        var game = store.Get(id);
        if (game is null) return false;
        var expectedTurn = game.Mode == GameMode.VsAi ? PlayerSymbol.X : (PlayerSymbol?)null;
        return store.Move(id, cell, expectedTurn);
    }
    public GameSnapshot? MakeAiMove(Guid id)
    {
        var game = store.Get(id);
        if (game is null || game.Mode != GameMode.VsAi || game.Status != GameStatus.InProgress || game.CurrentTurn != PlayerSymbol.O) return null;
        return store.Move(id, ai.ChooseMove(game.Cells), PlayerSymbol.O) ? store.Snapshot(id) : null;
    }
}
