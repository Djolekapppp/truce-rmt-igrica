using System;
using Godot;

/// <summary>
/// Prvi ekran: povezivanje na server, pa pravljenje ili ulazak u sobu.
/// Cim server posalje LobbyState, prelazimo u lobi.
/// </summary>
public partial class MainMenu : Control {
    private LineEdit _hostEdit;
    private LineEdit _portEdit;
    private LineEdit _usernameEdit;
    private Button _connectButton;
    private Button _disconnectButton;
    private Control _roomSection;
    private Button _createRoomButton;
    private LineEdit _roomIdEdit;
    private Button _joinRoomButton;
    private Label _statusLabel;

    private GameNet Net => GameNet.Instance;

    public override void _Ready() {
        _hostEdit = GetNode<LineEdit>("%HostEdit");
        _portEdit = GetNode<LineEdit>("%PortEdit");
        _usernameEdit = GetNode<LineEdit>("%UsernameEdit");
        _connectButton = GetNode<Button>("%ConnectButton");
        _disconnectButton = GetNode<Button>("%DisconnectButton");
        _roomSection = GetNode<Control>("%RoomSection");
        _createRoomButton = GetNode<Button>("%CreateRoomButton");
        _roomIdEdit = GetNode<LineEdit>("%RoomIdEdit");
        _joinRoomButton = GetNode<Button>("%JoinRoomButton");
        _statusLabel = GetNode<Label>("%StatusLabel");

        _connectButton.Pressed += OnConnectPressed;
        _disconnectButton.Pressed += OnDisconnectPressed;
        _createRoomButton.Pressed += OnCreateRoomPressed;
        _joinRoomButton.Pressed += OnJoinRoomPressed;
        _roomIdEdit.TextSubmitted += _ => OnJoinRoomPressed();

        Net.LobbyUpdated += OnLobbyUpdated;
        Net.InfoReceived += OnInfo;
        Net.ErrorReceived += OnError;
        Net.ConnectionLost += OnConnectionLost;

        if (Net.IsConnected) {
            _usernameEdit.Text = Net.Username;
        }

        RefreshUi();
        SetStatus(Net.IsConnected ? "Povezan. Napravi sobu ili udji u postojecu."
                                  : "Nisi povezan.");
    }

    public override void _ExitTree() {
        if (Net == null) {
            return;
        }

        Net.LobbyUpdated -= OnLobbyUpdated;
        Net.InfoReceived -= OnInfo;
        Net.ErrorReceived -= OnError;
        Net.ConnectionLost -= OnConnectionLost;
    }

    private void RefreshUi() {
        bool connected = Net.IsConnected;

        _hostEdit.Editable = !connected;
        _portEdit.Editable = !connected;
        _usernameEdit.Editable = !connected;
        _connectButton.Disabled = connected;
        _disconnectButton.Disabled = !connected;
        _roomSection.Visible = connected;
    }

    private async void OnConnectPressed() {
        string username = _usernameEdit.Text.Trim();

        if (username.Length == 0) {
            SetStatus("Unesi korisnicko ime.", true);
            return;
        }

        if (!int.TryParse(_portEdit.Text.Trim(), out int port) || port <= 0 || port > 65535) {
            SetStatus("Port nije validan.", true);
            return;
        }

        _connectButton.Disabled = true;
        SetStatus("Povezivanje...");

        string error = await Net.ConnectAsync(_hostEdit.Text.Trim(), port, username);

        if (error != null) {
            SetStatus("Povezivanje nije uspelo: " + error, true);
        } else {
            SetStatus("Povezan kao " + username + ".");
        }

        RefreshUi();
    }

    private void OnDisconnectPressed() {
        Net.Disconnect();
        RefreshUi();
        SetStatus("Veza prekinuta.");
    }

    private void OnCreateRoomPressed() {
        Net.CreateRoom();
    }

    private void OnJoinRoomPressed() {
        if (int.TryParse(_roomIdEdit.Text.Trim(), out int roomId)) {
            Net.JoinRoom(roomId);
        } else {
            SetStatus("ID sobe mora biti broj.", true);
        }
    }

    private void OnLobbyUpdated(Common.LobbyStateData _) {
        GetTree().ChangeSceneToFile("res://scenes/Lobby.tscn");
    }

    private void OnInfo(string text) => SetStatus(text);

    private void OnError(string text) => SetStatus("Greska: " + text, true);

    private void OnConnectionLost(string reason) {
        RefreshUi();
        SetStatus("Veza sa serverom je prekinuta: " + reason, true);
    }

    private void SetStatus(string text, bool isError = false) {
        _statusLabel.Text = text;
        _statusLabel.Modulate = isError ? new Color(1f, 0.45f, 0.42f) : Colors.White;
    }
}
