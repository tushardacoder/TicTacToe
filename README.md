# Threefold Tic Tac Toe

A browser based Tic Tac Toe game built with ASP.NET Core MVC, C#, JavaScript, and SignalR. Game state is held in memory; restarting the process clears all active games and rooms.

## Requirements

- .NET 10 SDK

## Run

```powershell
dotnet run --project src/TicTacToe.Web
```

Open the URL printed by ASP.NET Core. The home page supports local two player, player versus computer, and online rooms. Online rooms use the locally bundled SignalR JavaScript client and the hub at `/gameHub`. The web API is available under `/api/games`.

For online play, select **Online room** and create a room on one device. Open its invite link on a second device, or enter the room code there. X can move once the second player joins; O then takes a turn after X.

## Build and tests

```powershell
dotnet build TicTacToe.slnx
dotnet test TicTacToe.slnx
```

The Domain project contains the shared board rules and Minimax opponent. Application coordinates game actions, Infrastructure owns in-memory stores and online room state, and Web contains MVC, the browser UI, and SignalR hub.

## Current scope

- Implemented: solution foundation, shared game rules, game session store, local and AI play, online room and hub operations, reconnect token validation, disconnect timeout forfeits, and rematch agreement.
- SignalR room flow has been smoke tested with two independent client connections. A full manual browser accessibility and mobile review remains.
- Hosting on Monster ASP.NET and WebSocket support have not been verified. In-memory rooms do not survive a process restart and are not shared across multiple instances.
