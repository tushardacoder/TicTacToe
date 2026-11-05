using TicTacToe.Domain;
using TicTacToe.Infrastructure;

namespace TicTacToe.Infrastructure.Tests;

public class OnlineRoomManagerTests
{
    [Test]
    public void RoomAssignsSymbolsAndAllowsOnlyTheCurrentPlayerToMove()
    {
        var rooms=new OnlineRoomManager(); var x=rooms.Create("x-connection");
        Assert.That(rooms.Move(x.Code,"x-connection",0).Error, Is.EqualTo("Waiting for another player."));
        var o=rooms.Join(x.Code,"o-connection"); Assert.That(o, Is.Not.Null);
        Assert.That(o!.Symbol, Is.EqualTo(PlayerSymbol.O));
        Assert.That(rooms.Join(x.Code,"third-connection"), Is.Null);
        Assert.That(rooms.Move(x.Code,"o-connection",0).Game, Is.Null);
        Assert.That(rooms.Move(x.Code,"x-connection",0).Game?.MoveCount, Is.EqualTo(1));
        Assert.That(rooms.Move(x.Code,"o-connection",1).Game?.MoveCount, Is.EqualTo(2));
    }
    [Test]
    public void ReconnectRequiresPlayerTokenAndRestoresTheSameSlot()
    {
        var rooms=new OnlineRoomManager(); var x=rooms.Create("x-connection"); rooms.Join(x.Code,"o-connection");
        var oldTimer=rooms.StartDisconnectTimer(x.Code,PlayerSymbol.X);
        rooms.Disconnect("x-connection");
        Assert.That(rooms.Join(x.Code,"intruder"), Is.Null);
        Assert.That(rooms.Reconnect(x.Code,"wrong-token","new-x").Symbol, Is.Null);
        var restored=rooms.Reconnect(x.Code,x.ReconnectToken,"new-x");
        Assert.That(restored.Symbol, Is.EqualTo(PlayerSymbol.X));
        Assert.That(restored.Game?.Id, Is.EqualTo(x.Game.Id));
        Assert.That(rooms.Move(x.Code,"new-x",4).Game?.MoveCount, Is.EqualTo(1));
        Assert.That(oldTimer.IsCancellationRequested, Is.True);
        var newTimer=rooms.StartDisconnectTimer(x.Code,PlayerSymbol.X);
        rooms.RemoveDisconnectTimer(x.Code,PlayerSymbol.X,oldTimer);
        Assert.That(newTimer.IsCancellationRequested, Is.False);
        rooms.RemoveDisconnectTimer(x.Code,PlayerSymbol.X,newTimer);
    }
    [Test]
    public void DisconnectedPlayerCanForfeitOnce()
    {
        var rooms=new OnlineRoomManager(); var x=rooms.Create("x-connection"); var o=rooms.Join(x.Code,"o-connection");
        rooms.Disconnect("o-connection");
        Assert.That(rooms.ForfeitIfDisconnected(x.Code,PlayerSymbol.O), Is.True);
        Assert.That(rooms.ForfeitIfDisconnected(x.Code,PlayerSymbol.O), Is.False);
        Assert.That(rooms.Snapshot(x.Code)?.Result, Is.EqualTo(GameResult.PlayerXWinsByForfeit));
    }
    [Test]
    public void RematchNeedsBothPlayersAndResetsOnlyAfterAgreement()
    {
        var rooms=new OnlineRoomManager(); var x=rooms.Create("x-connection"); rooms.Join(x.Code,"o-connection");
        foreach (var move in new[] { ("x-connection",0), ("o-connection",3), ("x-connection",1), ("o-connection",4), ("x-connection",2) })
            Assert.That(rooms.Move(x.Code,move.Item1,move.Item2).Game, Is.Not.Null);
        Assert.That(rooms.RequestRematch(x.Code,"x-connection",out var waiting), Is.True);
        Assert.That(waiting?.Result, Is.EqualTo(GameResult.PlayerXWins));
        Assert.That(rooms.RequestRematch(x.Code,"o-connection",out var restarted), Is.True);
        Assert.That(restarted?.MoveCount, Is.Zero);
        Assert.That(restarted?.Status, Is.EqualTo(GameStatus.InProgress));
    }
    [Test]
    public void AbandonedWaitingRoomExpiresAndReleasesItsPlayerSlot()
    {
        var rooms=new OnlineRoomManager(); var x=rooms.Create("x-connection");
        Assert.That(rooms.CleanupExpired(DateTimeOffset.UtcNow.AddMinutes(16)), Is.EqualTo(1));
        Assert.That(rooms.Snapshot(x.Code), Is.Null);
        Assert.That(() => rooms.Create("x-connection"), Throws.Nothing);
    }
    [Test]
    public async Task ConcurrentRequestsConsumeOnlyOneTurn()
    {
        var rooms=new OnlineRoomManager(); var x=rooms.Create("x-connection"); rooms.Join(x.Code,"o-connection");
        var results=await Task.WhenAll(Enumerable.Range(0,9).Select(cell=>Task.Run(()=>rooms.Move(x.Code,"x-connection",cell))));
        Assert.That(results.Count(result=>result.Game is not null), Is.EqualTo(1));
        Assert.That(rooms.Snapshot(x.Code)?.MoveCount, Is.EqualTo(1));
    }
}
