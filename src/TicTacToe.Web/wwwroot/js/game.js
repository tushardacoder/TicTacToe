(() => {
  const cells = [...document.querySelectorAll("[data-cell]")];
  const status = document.getElementById("status");
  const start = document.getElementById("start");
  const restart = document.getElementById("restart");
  const rematch = document.getElementById("rematch");
  const modes = [...document.querySelectorAll("[data-mode]")];
  const onlineControls = document.getElementById("online-controls");
  const connectionState = document.getElementById("connection-state");
  const roomInvite = document.getElementById("room-invite");
  let gameId = null;
  let game = null;
  let mode = "Local";
  let roomCode = null;
  let playerSymbol = null;
  let roomReady = false;
  let opponentDisconnected = false;
  let rematchState = "idle";
  let busy = false;
  const symbol = value => value === 0 ? "X" : value === 1 ? "O" : null;
  const connection = new signalR.HubConnectionBuilder().withUrl("/gameHub").withAutomaticReconnect().build();

  function showError(error) { status.textContent = error?.message || "The action could not be completed."; }
  async function connect() {
    if (connection.state === signalR.HubConnectionState.Disconnected) await connection.start();
    connectionState.textContent = connection.state === signalR.HubConnectionState.Connected ? "Connected to online play" : "Connecting to online play…";
  }
  async function restoreRoom() {
    const code = sessionStorage.getItem("threefold-room");
    const token = sessionStorage.getItem("threefold-reconnect-token");
    if (!code || !token) return;
    try {
      await connect();
      const state = await connection.invoke("Reconnect", code, token);
      if (!state.success) throw new Error(state.error);
      mode = "Online"; modes.forEach(item => { const selected = item.dataset.mode === mode; item.classList.toggle("active", selected); item.setAttribute("aria-pressed", selected); });
      onlineControls.hidden = false; roomCode = code; playerSymbol = state.symbol; game = state.game; roomReady = state.ready; opponentDisconnected = !state.ready;
      setOnlineRoom(code); render(game);
    } catch { sessionStorage.removeItem("threefold-room"); sessionStorage.removeItem("threefold-reconnect-token"); }
  }
  connection.onreconnecting(() => { connectionState.textContent = "Connection lost. Trying to reconnect…"; });
  connection.onreconnected(async () => {
    connectionState.textContent = "Reconnected";
    const token = sessionStorage.getItem("threefold-reconnect-token");
    if (roomCode && token) {
      try {
        const state = await connection.invoke("Reconnect", roomCode, token);
        if (!state.success) { showError(state.error); return; }
        playerSymbol = state.symbol; roomReady = state.ready; opponentDisconnected = !state.ready; render(state.game);
      }
      catch (error) { showError(error); }
    }
  });
  connection.onclose(() => { connectionState.textContent = "Disconnected. Choose Online room to reconnect."; });
  connection.on("RoomReady", next => { game = next; roomReady = true; opponentDisconnected = false; rematchState = "idle"; render(next); status.textContent = "Both players are here. X starts."; });
  connection.on("GameUpdated", next => { game = next; render(next); });
  connection.on("PlayerDisconnected", who => { roomReady = false; opponentDisconnected = true; rematchState = "idle"; status.textContent = `${symbol(who)} left. They have 30 seconds to reconnect.`; render(game); });
  connection.on("PlayerReconnected", state => { roomReady = state.ready; opponentDisconnected = !state.ready; game = state.game; rematchState = "idle"; render(state.game); });
  connection.on("RematchStatus", payload => {
    const requestedBy = payload && typeof payload === "object" && "requestedBy" in payload ? payload.requestedBy : null;
    const next = payload && typeof payload === "object" && "game" in payload ? payload.game : payload;
    if (next) game = next;
    if (requestedBy === playerSymbol) {
      rematchState = "requested";
      status.textContent = "Rematch requested. Waiting for the other player.";
    }
    else if (requestedBy !== null && requestedBy !== undefined) {
      rematchState = "invited";
      status.textContent = "You are invited to a rematch.";
    }
    else if (game && game.status === 1 && game.moveCount === 0) {
      rematchState = "idle";
      status.textContent = "New round started. X starts.";
    }
    render(game);
  });

  function setOnlineRoom(code) {
    roomCode = code;
    roomInvite.hidden = false;
    roomInvite.textContent = `Room ${code} · Share this code or link: ${location.origin}/?room=${encodeURIComponent(code)}`;
    document.getElementById("room-code").value = code;
    document.getElementById("create-room").hidden = true;
    document.getElementById("join-form").hidden = true;
    restart.hidden = true; start.hidden = true;
  }
  function resultText(result) {
    if (result === null) return null;
    if (result === 2) return "It's a draw. Well played.";
    const winner = result === 0 || result === 3 ? 0 : result === 1 || result === 4 ? 1 : null;
    if (mode === "Online" && winner !== null) {
      const winnerName = symbol(winner);
      return playerSymbol === winner ? `${winnerName} wins! You win.` : `${winnerName} wins. You lose.`;
    }
    if (mode === "VsAi" && result === 0) return "You win!";
    if (mode === "VsAi" && result === 1) return "You lose.";
    if (result === 3) return "Player X wins by forfeit.";
    if (result === 4) return "Player O wins by forfeit.";
    return `${symbol(result)} wins. Well played.`;
  }
  function render(next) {
    game = next;
    const current = symbol(next.currentTurn);
    const result = resultText(next.result);
    if (rematchState === "invited") {
      status.textContent = "You are invited to a rematch.";
    }
    else if (rematchState === "requested") {
      status.textContent = "Rematch requested. Waiting for the other player.";
    }
    else if (result) status.textContent = result;
    else if (mode === "Online" && !roomReady) status.textContent = opponentDisconnected ? "Opponent disconnected. Waiting for a reconnect." : "Waiting for another player to join.";
    else if (mode === "Online") status.textContent = current === symbol(playerSymbol) ? `Your turn (${current})` : `Waiting for ${current}`;
    else if (mode === "VsAi") status.textContent = current === "X" ? "Your turn" : "Computer's turn";
    else status.textContent = `Player ${current}'s turn`;
    const line = next.winningLine || [];
    cells.forEach((cell, index) => {
      const mark = symbol(next.cells[index]);
      cell.textContent = mark || "";
      cell.dataset.symbol = mark || "";
      cell.classList.toggle("winning", line.includes(index));
      const wrongTurn = mode === "Online" && (!roomReady || current !== symbol(playerSymbol));
      cell.disabled = busy || !game || mark !== null || next.status !== 1 || wrongTurn;
      cell.setAttribute("aria-label", `${mark ? `${mark}, ` : ""}square ${index + 1}${line.includes(index) ? ", winning square" : ""}`);
    });
    const ended = next.status === 2;
    restart.hidden = mode === "Online" || !ended;
    rematch.hidden = mode !== "Online" || !ended;
    if (mode === "Online" && ended) {
      if (rematchState === "invited") {
        rematch.hidden = false;
        rematch.disabled = false;
        rematch.textContent = "Join rematch";
      }
      else if (rematchState === "requested") {
        rematch.hidden = false;
        rematch.disabled = true;
        rematch.textContent = "Rematch requested";
      }
      else {
        rematch.hidden = false;
        rematch.disabled = false;
        rematch.textContent = "Rematch";
      }
    }
  }
  async function newGame() {
    if (busy) return;
    busy = true;
    try { const next = await request("/api/games", { mode: mode === "VsAi" ? 1 : 0 }); gameId = next.id; roomCode = null; game = next; render(next); restart.hidden = false; start.hidden = true; }
    catch (error) { showError(error); }
    finally { busy = false; if (game) render(game); }
  }
  async function request(url, body) {
    const response = await fetch(url, { method: body ? "POST" : "GET", headers: { "Content-Type": "application/json" }, body: body ? JSON.stringify(body) : undefined });
    const data = await response.json();
    if (!response.ok) throw new Error(data.error || "The game could not be updated.");
    return data;
  }
  modes.forEach(button => button.addEventListener("click", async () => {
    if (busy) return;
    if (roomCode && mode === "Online" && button.dataset.mode !== "Online") {
      modes.forEach(item => { const selected = item.dataset.mode === mode; item.classList.toggle("active", selected); item.setAttribute("aria-pressed", selected); });
      return;
    }
    mode = button.dataset.mode;
    modes.forEach(item => { const selected = item === button; item.classList.toggle("active", selected); item.setAttribute("aria-pressed", selected); });
    onlineControls.hidden = mode !== "Online";
    if (mode !== "Online") { gameId = null; game = null; cells.forEach(cell => { cell.textContent = ""; cell.disabled = true; cell.classList.remove("winning"); }); status.textContent = "Choose a mode to begin."; }
    else if (!roomCode) { gameId = null; game = null; cells.forEach(cell => { cell.textContent = ""; cell.disabled = true; cell.classList.remove("winning"); }); status.textContent = "Create a room or join with a code."; }
    start.hidden = mode === "Online";
    restart.hidden = mode === "Online" || !game || game.status !== 2;
    if (mode === "Online") {
      try { await connect(); const token = sessionStorage.getItem("threefold-reconnect-token"); const code = sessionStorage.getItem("threefold-room"); if (token && code && !roomCode) await restoreRoom(); }
      catch (error) { connectionState.textContent = error.message; }
    }
  }));
  document.getElementById("create-room").addEventListener("click", async () => {
    try {
      await connect(); const result = await connection.invoke("CreateRoom");
      if (!result.success) { connectionState.textContent = result.error; return; }
      roomCode = result.code; playerSymbol = result.symbol; game = result.game; roomReady = result.ready; opponentDisconnected = false;
      sessionStorage.setItem("threefold-room", roomCode); sessionStorage.setItem("threefold-reconnect-token", result.reconnectToken);
      setOnlineRoom(roomCode); render(game); status.textContent = "Room created. Open the invite link on another device to join.";
    } catch (error) { connectionState.textContent = error.message; }
  });
  document.getElementById("join-form").addEventListener("submit", async event => {
    event.preventDefault();
    const code = document.getElementById("room-code").value.trim().toUpperCase();
    try {
      await connect(); const result = await connection.invoke("JoinRoom", code);
      if (!result.success) { connectionState.textContent = result.error; return; }
      roomCode = result.code; playerSymbol = result.symbol; game = result.game; roomReady = result.ready; opponentDisconnected = false;
      sessionStorage.setItem("threefold-room", roomCode); sessionStorage.setItem("threefold-reconnect-token", result.reconnectToken);
      setOnlineRoom(roomCode); render(game);
    } catch (error) { connectionState.textContent = error.message; }
  });
  async function playCell(index) {
    if (busy || !game) return;
    busy = true; cells.forEach(item => item.disabled = true);
    try {
      if (mode === "Online") {
        const result = await connection.invoke("MakeMove", roomCode, index);
        if (!result.success) showError(result.error);
      }
      else if (gameId) {
        const activeGameId = gameId;
        game = await request(`/api/games/${activeGameId}/moves`, { cellIndex: index });
        render(game);
        if (mode === "VsAi" && game.status === 1 && game.currentTurn === 1) {
          status.textContent = "Computer is thinking…";
          await new Promise(resolve => setTimeout(resolve, 1000));
          game = await request(`/api/games/${activeGameId}/ai-moves`, {});
          render(game);
        }
      }
    } catch (error) { showError(error); }
    finally {
      busy = false;
      if (mode !== "Online" && gameId) { try { game = await request(`/api/games/${gameId}`); } catch { } }
      if (game) render(game);
    }
  }
  cells.forEach(cell => cell.addEventListener("click", () => playCell(Number(cell.dataset.cell))));
  start.addEventListener("click", newGame);
  restart.addEventListener("click", newGame);
  rematch.addEventListener("click", async () => {
    try {
      const result = await connection.invoke("RequestRematch", roomCode);
      if (!result.success) { showError(result.error); return; }
      rematchState = "requested";
      render(game);
    }
    catch (error) { showError(error); }
  });
  const invitation = new URLSearchParams(location.search).get("room");
  if (invitation) {
    const savedRoom = sessionStorage.getItem("threefold-room");
    if (savedRoom && savedRoom.toUpperCase() !== invitation.toUpperCase()) { sessionStorage.removeItem("threefold-room"); sessionStorage.removeItem("threefold-reconnect-token"); }
    mode = "Online"; modes.find(item => item.dataset.mode === mode)?.click(); document.getElementById("room-code").value = invitation;
  }
  restoreRoom();
})();

