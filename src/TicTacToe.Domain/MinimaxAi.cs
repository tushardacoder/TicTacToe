namespace TicTacToe.Domain;

public sealed class MinimaxAi
{
    public int ChooseMove(IReadOnlyList<PlayerSymbol?> cells, PlayerSymbol ai = PlayerSymbol.O)
    {
        var board = cells.ToArray(); var best = -2; var choice = -1;
        for (var i = 0; i < 9; i++) if (board[i] is null) { board[i] = ai; var score = Score(board, ai, false); board[i] = null; if (score > best) { best = score; choice = i; } }
        return choice;
    }
    private static int Score(PlayerSymbol?[] b, PlayerSymbol ai, bool maximizing)
    {
        int[][] wins = [[0,1,2],[3,4,5],[6,7,8],[0,3,6],[1,4,7],[2,5,8],[0,4,8],[2,4,6]];
        if (wins.Any(w => w.All(i => b[i] == ai))) return 1;
        var human = ai == PlayerSymbol.X ? PlayerSymbol.O : PlayerSymbol.X;
        if (wins.Any(w => w.All(i => b[i] == human))) return -1;
        if (b.All(c => c is not null)) return 0;
        var best = maximizing ? -2 : 2;
        for (var i=0;i<9;i++) if (b[i] is null) { b[i] = maximizing ? ai : human; var s=Score(b, ai, !maximizing); b[i]=null; best=maximizing ? Math.Max(best,s) : Math.Min(best,s); }
        return best;
    }
}
