using System.Collections.Generic;
using Common;
using Godot;

/// <summary>
/// Lobi: svaki igrac bira rasu i potvrdjuje spremnost. Rasa se rezervise tek
/// kad se posalje Ready, pa server ima poslednju rec o duplikatima — ovaj UI
/// samo unapred zakljucava ono sto je vec zauzeto.
///
/// Start Game je aktivan tek kad su sva tri mesta popunjena i svi spremni.
/// </summary>
public partial class Lobby : Control {
    private const int MaxPlayers = 3;

    private Label _roomLabel;
    private Button _leaveButton;
    private VBoxContainer _playerList;
    private VBoxContainer _factionList;
    private Button _readyButton;
    private Button _startButton;
    private Label _hintLabel;
    private RichTextLabel _log;
    private LineEdit _chatEdit;
    private Button _chatSend;

    private readonly Dictionary<string, Button> _factionButtons = new();
    private string _selectedFaction = "";

    private GameNet Net => GameNet.Instance;

    public override void _Ready() {
        _roomLabel = GetNode<Label>("%RoomLabel");
        _leaveButton = GetNode<Button>("%LeaveButton");
        _playerList = GetNode<VBoxContainer>("%PlayerList");
        _factionList = GetNode<VBoxContainer>("%FactionList");
        _readyButton = GetNode<Button>("%ReadyButton");
        _startButton = GetNode<Button>("%StartButton");
        _hintLabel = GetNode<Label>("%HintLabel");
        _log = GetNode<RichTextLabel>("%Log");
        _chatEdit = GetNode<LineEdit>("%ChatEdit");
        _chatSend = GetNode<Button>("%ChatSend");

        BuildFactionButtons();

        _leaveButton.Pressed += OnLeavePressed;
        _readyButton.Pressed += OnReadyPressed;
        _startButton.Pressed += OnStartPressed;
        _chatSend.Pressed += OnChatSend;
        _chatEdit.TextSubmitted += _ => OnChatSend();

        Net.LobbyUpdated += OnLobbyUpdated;
        Net.GameStateUpdated += OnGameStateUpdated;
        Net.ChatReceived += OnChat;
        Net.InfoReceived += OnInfo;
        Net.ErrorReceived += OnError;
        Net.ConnectionLost += OnConnectionLost;

        Refresh();
    }

    public override void _ExitTree() {
        if (Net == null) {
            return;
        }

        Net.LobbyUpdated -= OnLobbyUpdated;
        Net.GameStateUpdated -= OnGameStateUpdated;
        Net.ChatReceived -= OnChat;
        Net.InfoReceived -= OnInfo;
        Net.ErrorReceived -= OnError;
        Net.ConnectionLost -= OnConnectionLost;
    }

    // --- izgradnja UI-ja -------------------------------------------------

    private void BuildFactionButtons() {
        foreach (var id in Factions.All) {
            var button = new Button {
                ToggleMode = true,
                CustomMinimumSize = new Vector2(0, 52),
                TooltipText = Factions.Motto(id),
                Alignment = HorizontalAlignment.Left,
            };

            button.AddThemeColorOverride("font_color", Factions.Tint(id));

            // expand_icon je bitan: bez njega bi minimalna sirina dugmeta
            // porasla na punu velicinu teksture.
            button.Icon = Factions.Icon(id);
            button.ExpandIcon = true;
            button.AddThemeConstantOverride("icon_max_width", 28);

            foreach (var state in new[] { "normal", "pressed", "hover", "focus" }) {
                button.AddThemeColorOverride($"icon_{state}_color", Factions.IconColor);
            }

            // Zauzeta rasa je zakljucana, pa joj se i ikonica prigusi.
            button.AddThemeColorOverride("icon_disabled_color",
                new Color(1f, 1f, 1f, 0.35f));

            string factionId = id;
            button.Pressed += () => OnFactionPressed(factionId);

            _factionButtons[id] = button;
            _factionList.AddChild(button);
        }
    }

    private Control BuildPlayerRow(LobbyPlayer player, bool isMe) {
        var panel = new PanelContainer();

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        // Ikonica rase ispred imena. Dok rasa nije izabrana ostaje prazno
        // mesto iste sirine, pa se redovi ne pomeraju.
        row.AddChild(Factions.IconRect(player.Class, 28));

        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(names);

        var nameLabel = new Label {
            Text = isMe ? player.Username + "  (ti)" : player.Username,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 16);
        names.AddChild(nameLabel);

        var factionLabel = new Label {
            Text = Factions.IsValid(player.Class)
                ? Factions.DisplayName(player.Class) + " - " + Factions.ResourceName(player.Class)
                : "jos nije izabrao rasu",
            Modulate = new Color(1f, 1f, 1f, 0.65f),
        };
        factionLabel.AddThemeFontSizeOverride("font_size", 12);
        names.AddChild(factionLabel);

        var readyLabel = new Label {
            Text = player.Ready ? "SPREMAN" : "ceka",
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = player.Ready
                ? new Color(0.45f, 0.85f, 0.5f)
                : new Color(1f, 1f, 1f, 0.45f),
        };
        row.AddChild(readyLabel);

        return panel;
    }

    private Control BuildEmptySlot() {
        var panel = new PanelContainer { Modulate = new Color(1f, 1f, 1f, 0.35f) };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        margin.AddChild(new Label { Text = "prazno mesto - ceka se igrac" });
        return panel;
    }

    // --- osvezavanje stanja ----------------------------------------------

    private void Refresh() {
        var players = Net.LobbyPlayers;
        var me = Net.FindMe();
        bool iAmReady = me != null && me.Ready;

        // Kad server potvrdi nasu rasu, lokalni izbor se poravna sa njom.
        if (me != null && Factions.IsValid(me.Class)) {
            _selectedFaction = me.Class;
        }

        _roomLabel.Text = Net.RoomId >= 0
            ? $"Soba #{Net.RoomId}   -   igraci {players.Count}/{MaxPlayers}"
            : "Soba -";

        RefreshPlayerList(players, me);
        RefreshFactionButtons(players, me, iAmReady);

        _readyButton.Text = iAmReady ? "Ponisti spremnost" : "Spreman sam";
        _readyButton.Disabled = !iAmReady && !Factions.IsValid(_selectedFaction);

        bool everyoneReady = players.Count == MaxPlayers;
        foreach (var player in players) {
            if (!player.Ready) {
                everyoneReady = false;
            }
        }

        _startButton.Disabled = !everyoneReady;
        _hintLabel.Text = BuildHint(players.Count, iAmReady, everyoneReady);
    }

    private void RefreshPlayerList(IReadOnlyList<LobbyPlayer> players, LobbyPlayer me) {
        foreach (var child in _playerList.GetChildren()) {
            _playerList.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var player in players) {
            _playerList.AddChild(BuildPlayerRow(player, me != null && player.Id == me.Id));
        }

        for (int i = players.Count; i < MaxPlayers; i++) {
            _playerList.AddChild(BuildEmptySlot());
        }
    }

    private void RefreshFactionButtons(IReadOnlyList<LobbyPlayer> players, LobbyPlayer me, bool iAmReady) {
        foreach (var id in Factions.All) {
            string takenBy = null;

            foreach (var player in players) {
                bool isMe = me != null && player.Id == me.Id;
                if (!isMe && player.Class == id) {
                    takenBy = player.Username;
                }
            }

            var button = _factionButtons[id];
            bool taken = takenBy != null;

            button.Text = taken
                ? $"  {Factions.DisplayName(id)}   (zauzeo {takenBy})"
                : $"  {Factions.DisplayName(id)}   -   {Factions.ResourceName(id)}";

            // Dok si spreman, izbor je zakljucan: prvo se ponistava spremnost.
            button.Disabled = taken || iAmReady;
            button.SetPressedNoSignal(_selectedFaction == id);
        }
    }

    private string BuildHint(int playerCount, bool iAmReady, bool everyoneReady) {
        if (everyoneReady) {
            return "Svi su spremni - partija moze da pocne.";
        }

        if (playerCount < MaxPlayers) {
            return $"Ceka se jos {MaxPlayers - playerCount} igraca. Podeli ID sobe: {Net.RoomId}";
        }

        return iAmReady
            ? "Cekaju se ostali igraci."
            : "Izaberi rasu pa potvrdi da si spreman.";
    }

    // --- akcije ----------------------------------------------------------

    private void OnFactionPressed(string factionId) {
        _selectedFaction = factionId;
        Refresh();
    }

    private void OnReadyPressed() {
        var me = Net.FindMe();

        if (me != null && me.Ready) {
            Net.SendUnready();
            return;
        }

        if (!Factions.IsValid(_selectedFaction)) {
            AppendLog("Prvo izaberi rasu.", true);
            return;
        }

        Net.SendReady(_selectedFaction);
    }

    private void OnStartPressed() {
        Net.StartGame();
    }

    private void OnLeavePressed() {
        Net.LeaveRoom();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    private void OnChatSend() {
        string text = _chatEdit.Text.Trim();

        if (text.Length == 0) {
            return;
        }

        Net.SendChat(text);
        _chatEdit.Clear();
    }

    // --- dogadjaji sa mreze ----------------------------------------------

    private void OnLobbyUpdated(LobbyStateData _) => Refresh();

    private void OnGameStateUpdated(GameStateData state) {
        if (state.Turn > 0) {
            GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
        }
    }

    private void OnChat(string content) => AppendLog(content);

    private void OnInfo(string content) => AppendLog(content);

    private void OnError(string message) => AppendLog(message, true);

    private void OnConnectionLost(string reason) {
        AppendLog("Veza sa serverom je prekinuta: " + reason, true);
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    private void AppendLog(string text, bool isError = false) {
        _log.AppendText(isError
            ? $"[color=#ff7066]{text}[/color]\n"
            : text + "\n");
    }
}
