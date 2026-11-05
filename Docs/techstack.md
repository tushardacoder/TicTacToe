# Tic Tac Toe — TechStack Documentation

> **Purpose:** Detailed implementation guide based on the uploaded *Tic Tac Toe Product Requirement Document (PRD), Version 1.0*.
>
> **Important distinction:** The PRD specifies the game requirements and high-level technical constraints. CQRS, Cortex.Mediator, Clean Architecture project boundaries, and Serilog are explained below as a **recommended implementation design**. They should only be described as implemented technologies after they have actually been added to the solution.

---

## 1. Project Overview

**Tic Tac Toe** is a browser-based game built with ASP.NET Core MVC, C#, HTML, CSS, JavaScript, and ASP.NET Core SignalR.

It supports three game modes:

1. **Local two-player:** Two people play in the same browser.
2. **Player versus AI:** A human plays against a Minimax-based AI.
3. **Online multiplayer:** Two players play in real time through SignalR rooms.

The game does not require a database. Game sessions, rooms, player slots, and temporary reconnection information are held in application memory.

### 1.1 Technology and responsibility

| Technology | Responsibility |
|---|---|
| ASP.NET Core MVC | HTTP endpoints, page delivery, dependency injection, application hosting |
| C# | Game rules, application use cases, room management, AI |
| HTML/CSS/JavaScript | Browser UI and board interaction |
| ASP.NET Core SignalR | Real-time online game communication |
| In-memory collections | Temporary rooms and active game sessions |
| Minimax | AI move selection |
| NUnit | Automated tests |
| Moq | Test doubles for dependencies |
| Shouldly | Readable assertions |
| Cortex.Mediator | Recommended mediator implementation for commands and queries |
| Serilog | Recommended structured logging implementation |
| Monster ASP.NET | Intended hosting target |

**No database is required by the PRD.** Do not add Entity Framework Core, SQL Server, repositories, or Unit of Work merely to make the architecture look more complex. In-memory state is an intentional requirement, with the hosting/restart limitation explained later.

