using Microsoft.AspNetCore.Mvc;
using TicTacToe.Application;
using TicTacToe.Domain;

namespace TicTacToe.Web.Controllers;

[ApiController]
[Route("api/games")]
public sealed class GameController(GameService games, ILogger<GameController> logger) : ControllerBase
{
    [HttpPost]
    public IActionResult Create([FromBody] CreateGameRequest request)
    {
        if (!Enum.IsDefined(request.Mode) || request.Mode == GameMode.Online) return BadRequest(new { error = "Choose local or AI play." });
        var game = games.Create(request.Mode);
        logger.LogInformation("Created {GameMode} game {GameId}.", game.Mode, game.Id);
        return Ok(game);
    }
    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id) => games.Get(id) is { } game ? Ok(game) : NotFound(new { error = "Game not found." });
    [HttpPost("{id:guid}/moves")]
    public IActionResult Move(Guid id, [FromBody] MoveRequest request)
    {
        if (!games.Move(id, request.CellIndex))
        {
            logger.LogWarning("Rejected move for game {GameId} at cell {CellIndex}.", id, request.CellIndex);
            return Conflict(new { error = "That move is unavailable." });
        }
        logger.LogInformation("Accepted move for game {GameId} at cell {CellIndex}.", id, request.CellIndex);
        return Get(id);
    }

    [HttpPost("{id:guid}/ai-moves")]
    public IActionResult AiMove(Guid id)
    {
        var game = games.MakeAiMove(id);
        if (game is null) return Conflict(new { error = "The computer cannot move right now." });
        logger.LogInformation("Accepted computer move for game {GameId}.", id);
        return Ok(game);
    }
}
public sealed record CreateGameRequest(GameMode Mode);
public sealed record MoveRequest(int CellIndex);
