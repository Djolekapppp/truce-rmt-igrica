using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Godot;

/// <summary>
/// Autoload singleton. Jedina tacka kroz koju scene pricaju sa serverom.
///
/// Poruke se citaju na pozadinskom tasku i guraju u red, a red se prazni u
/// _Process. Zato se svi event handleri izvrsavaju na glavnom threadu i smeju
/// slobodno da diraju UI.
/// </summary>
public partial class GameNet : Node {
    public static GameNet Instance { get; private set; }

    private readonly ConcurrentQueue<IGameMessage> _incoming = new();
    private readonly List<LobbyPlayer> _lobbyPlayers = new();

    private CancellationTokenSource _cts;
    private volatile bool _connected;
    private volatile string _pendingDisconnect;

    public string Username { get; private set; } = "";
    public uint MyPlayerId { get; private set; }
    public bool HasPlayerId { get; private set; }
    public int RoomId { get; private set; } = -1;
    public bool IsConnected => _connected;

    public IReadOnlyList<LobbyPlayer> LobbyPlayers => _lobbyPlayers;
    public GameStateData GameState { get; private set; } = new();
    public List<string> Hand { get; private set; } = new();

    public event Action<LobbyStateData> LobbyUpdated;
    public event Action<GameStateData> GameStateUpdated;
    // public event Action<ModifierData> ModifierAdded;
    public event Action<List<string>> HandUpdated;
    public event Action<string> ChatReceived;
    public event Action<string> InfoReceived;
    public event Action<string> ErrorReceived;
    public event Action GameOverReceived;
    public event Action<string> ConnectionLost;

    public override void _EnterTree() {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    // --- konekcija -------------------------------------------------------

    /// <summary>Vraca null ako je povezivanje uspelo, inace poruku o gresci.</summary>
    public async Task<string> ConnectAsync(string host, int port, string username) {
        if (_connected) {
            return "Vec si povezan.";
        }

        try {
            await Task.Run(() => Communication.Instance.Connect(host, port));
        } catch (Exception ex) {
            return ex.Message;
        }

        Username = username;
        _connected = true;
        _pendingDisconnect = null;
        _cts = new CancellationTokenSource();

        var token = _cts.Token;
        _ = Task.Run(() => ReceiveLoop(token), token);

        Send(new ConnectMessage { Data = new ConnectMessageData { Content = username } });
        return null;
    }

    public void Disconnect() {
        _cts?.Cancel();
        _cts = null;
        _connected = false;

        Communication.Instance.Close();

        HasPlayerId = false;
        RoomId = -1;
        _lobbyPlayers.Clear();
        Hand.Clear();
        GameState = new GameStateData();

        while (_incoming.TryDequeue(out _)) { }
    }

    private async Task ReceiveLoop(CancellationToken token) {
        try {
            while (!token.IsCancellationRequested) {
                var message = await Communication.Instance.ReceiveMessageAsync();
                if (message != null) {
                    _incoming.Enqueue(message);
                }
            }
        } catch (Exception ex) {
            if (!token.IsCancellationRequested) {
                _connected = false;
                _pendingDisconnect = ex.Message;
            }
        }
    }

    // --- slanje ----------------------------------------------------------

    public bool Send(IGameMessage message) {
        if (!_connected) {
            ErrorReceived?.Invoke("Nisi povezan na server.");
            return false;
        }

        try {
            Communication.Instance.SendMessage(message);
            return true;
        } catch (Exception ex) {
            _connected = false;
            _pendingDisconnect = ex.Message;
            return false;
        }
    }

    public void CreateRoom() => Send(new CreateRoomMessage());

    public void JoinRoom(int roomId) =>
        Send(new JoinRoomMessage { Data = new JoinRoomData { RoomId = roomId } });

    public void LeaveRoom() {
        if (Send(new LeaveRoomMessage())) {
            RoomId = -1;
            _lobbyPlayers.Clear();
        }
    }

    public void SendReady(string factionId) =>
        Send(new ReadyMessage { Data = new ReadyData { Class = factionId } });

    public void SendUnready() => Send(new UnreadyMessage());

    /// <summary>Seed 0 znaci "server neka izabere nasumicno".</summary>
    public void StartGame(ulong seed = 0) =>
        Send(new StartGameMessage { Data = new StartGameData { Seed = seed } });

    public void PlayCard(string cardKey) =>
        Send(new CardMessage { Data = new CardData { Name = cardKey } });

    public void SendChat(string content) =>
        Send(new ChatMessage { Data = new ChatData { Content = content } });

    // --- pumpa poruka ----------------------------------------------------

    public override void _Process(double delta) {
        while (_incoming.TryDequeue(out var message)) {
            Dispatch(message);
        }

        var reason = _pendingDisconnect;
        if (reason != null) {
            _pendingDisconnect = null;
            Disconnect();
            ConnectionLost?.Invoke(reason);
        }
    }

    private void Dispatch(IGameMessage message) {
        switch (message) {
            case WelcomeMessage welcome:
                MyPlayerId = welcome.Data.PlayerId;
                HasPlayerId = true;
                break;

            case LobbyStateMessage lobby:
                RoomId = (int)lobby.Data.RoomId;
                _lobbyPlayers.Clear();
                _lobbyPlayers.AddRange(lobby.Data.Players);
                LobbyUpdated?.Invoke(lobby.Data);
                break;

            case GameStateMessage state:
                GameState = state.Data;
                GameStateUpdated?.Invoke(state.Data);
                break;

            // case ModifierMessage modifier:
            //     ModifierAdded?.Invoke(modifier.Data);
            //     break;

            case HandMessage hand:
                Hand = hand.Data.Cards;
                HandUpdated?.Invoke(hand.Data.Cards);
                break;

            case ChatMessage chat:
                ChatReceived?.Invoke(chat.Data.Content);
                break;

            case GameOverMessage:
                GameOverReceived?.Invoke();
                break;

            case ResponseMessage response:
                InfoReceived?.Invoke(response.Data.Content);
                break;

            case ErrorMessage error:
                ErrorReceived?.Invoke(error.Data.Message);
                break;

            default:
                GD.Print($"[GameNet] Neobradjena poruka: {message.GetType().Name}");
                break;
        }
    }

    /// <summary>Igrac iz poslednjeg LobbyState-a koji odgovara ovom klijentu.</summary>
    public LobbyPlayer FindMe() {
        if (!HasPlayerId) {
            return null;
        }

        foreach (var player in _lobbyPlayers) {
            if (player.Id == MyPlayerId) {
                return player;
            }
        }

        return null;
    }
}
