using TicTacToe.Domain;

namespace TicTacToe.Domain.Tests;

public class GameSessionTests
{
    [Test] public void NewGameStartsWithXAndNineEmptyCells()
    {
        var game=GameSession.Create(GameMode.Local);
        Assert.That(game.CurrentTurn, Is.EqualTo(PlayerSymbol.X));
        Assert.That(game.Cells, Has.Count.EqualTo(9));
        Assert.That(game.Cells, Has.All.Null);
    }
    [TestCase(-1)] [TestCase(9)] public void InvalidIndexDoesNotChangeGame(int index)
    {
        var game=GameSession.Create(GameMode.Local);
        Assert.That(game.TryMove(index), Is.False);
        Assert.That(game.MoveCount, Is.Zero);
    }
    [Test] public void OccupiedCellCannotBePlayedTwice()
    {
        var game=GameSession.Create(GameMode.Local);
        Assert.That(game.TryMove(0), Is.True);
        Assert.That(game.TryMove(0), Is.False);
        Assert.That(game.MoveCount, Is.EqualTo(1));
    }
    [TestCase(new[]{0,1,2})] [TestCase(new[]{3,4,5})] [TestCase(new[]{6,7,8})]
    [TestCase(new[]{0,3,6})] [TestCase(new[]{1,4,7})] [TestCase(new[]{2,5,8})]
    [TestCase(new[]{0,4,8})] [TestCase(new[]{2,4,6})]
    public void DetectsEveryWinningLine(int[] line)
    {
        var game=GameSession.Create(GameMode.Local);
        var used=new HashSet<int>(line);
        var filler=Enumerable.Range(0,9).Where(i=>!used.Contains(i)).GetEnumerator();
        foreach(var cell in line) { game.TryMove(cell); if(cell!=line[^1]) { Assert.That(filler.MoveNext(), Is.True); game.TryMove(filler.Current); } }
        Assert.That(game.Result, Is.EqualTo(GameResult.PlayerXWins));
        Assert.That(game.WinningLine, Is.EquivalentTo(line));
    }
    [Test] public void RejectsMovesAfterWin()
    {
        var game=GameSession.Create(GameMode.Local);
        foreach(var i in new[]{0,3,1,4,2}) game.TryMove(i);
        Assert.That(game.TryMove(8), Is.False);
    }
    [Test] public void DetectsDraw()
    {
        var game=GameSession.Create(GameMode.Local);
        foreach(var i in new[]{0,1,2,4,3,5,7,6,8}) game.TryMove(i);
        Assert.That(game.Result, Is.EqualTo(GameResult.Draw));
    }
    [Test] public void MinimaxTakesImmediateWinAndBlocksOpponent()
    {
        var ai=new MinimaxAi();
        Assert.That(ai.ChooseMove(new PlayerSymbol?[]{PlayerSymbol.O,PlayerSymbol.O,null,PlayerSymbol.X,PlayerSymbol.X,null,null,null,null}), Is.EqualTo(2));
        Assert.That(ai.ChooseMove(new PlayerSymbol?[]{PlayerSymbol.X,PlayerSymbol.X,null,PlayerSymbol.O,null,null,null,null,null}), Is.EqualTo(2));
    }
}
