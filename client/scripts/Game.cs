using System.Collections.Generic;
using Common;
using Godot;

/// <summary>
/// Ekran partije.
///
/// Traka epoha pokazuje svih sest epoha i ko je u kojoj u zlatnom a ko u
/// mracnom dobu. Od 4. epohe svaki igrac ima svoj modifikator, pa se na
/// kartama prikazuje i osnovna i modifikovana vrednost.
/// </summary>
public partial class Game : Control {
    private Label _epochLabel;
    private Label _ageLabel;
    private Label _turnLabel;
    private Label _factionLabel;
    private Button _deckButton;
    private HBoxContainer _epochStrip;
    private Label _elvesLabel;
    private Label _dwarvesLabel;
    private Label _humansLabel;
    private PanelContainer _modifierPanel;
    private Label _modifierTitle;
    private Label _modifierDesc;
    private Label _hintLabel;
    private HBoxContainer _handBox;
    private RichTextLabel _log;

    private Control _deckOverlay;
    private Label _deckTitle;
    private Label _deckHint;
    private Button _deckClose;
    private GridContainer _deckGrid;

    private Control _endOverlay;
    private Label _resultTitle;
    private Label _resultText;
    private HBoxContainer _scoreRow;
    private Button _playAgainButton;
    private Button _exitButton;
    private Label _endHint;

    private readonly List<Button> _playButtons = new();
    private readonly List<PanelContainer> _epochCells = new();

    private string _myFaction = "";
    private uint _lastTurn;
    private int _handCount;
    private bool _waitingForOthers;
    private bool _gameOver;

    private GameNet Net => GameNet.Instance;

    public override void _Ready() {
        _epochLabel = GetNode<Label>("%EpochLabel");
        _ageLabel = GetNode<Label>("%AgeLabel");
        _turnLabel = GetNode<Label>("%TurnLabel");
        _factionLabel = GetNode<Label>("%FactionLabel");
        _deckButton = GetNode<Button>("%DeckButton");
        _epochStrip = GetNode<HBoxContainer>("%EpochStrip");
        _elvesLabel = GetNode<Label>("%ElvesLabel");
        _dwarvesLabel = GetNode<Label>("%DwarvesLabel");
        _humansLabel = GetNode<Label>("%HumansLabel");
        _modifierPanel = GetNode<PanelContainer>("%ModifierPanel");
        _modifierTitle = GetNode<Label>("%ModifierTitle");
        _modifierDesc = GetNode<Label>("%ModifierDesc");
        _hintLabel = GetNode<Label>("%HintLabel");
        _handBox = GetNode<HBoxContainer>("%HandBox");
        _log = GetNode<RichTextLabel>("%Log");

        _deckOverlay = GetNode<Control>("%DeckOverlay");
        _deckTitle = GetNode<Label>("%DeckTitle");
        _deckHint = GetNode<Label>("%DeckHint");
        _deckClose = GetNode<Button>("%DeckClose");
        _deckGrid = GetNode<GridContainer>("%DeckGrid");

        _endOverlay = GetNode<Control>("%EndOverlay");
        _resultTitle = GetNode<Label>("%ResultTitle");
        _resultText = GetNode<Label>("%ResultText");
        _scoreRow = GetNode<HBoxContainer>("%ScoreRow");
        _playAgainButton = GetNode<Button>("%PlayAgainButton");
        _exitButton = GetNode<Button>("%ExitButton");
        _endHint = GetNode<Label>("%EndHint");

        var me = Net.FindMe();
        _myFaction = me != null ? me.Class : "";
        _lastTurn = Net.GameState.Turn;

        BuildEpochStrip();

        _deckButton.Pressed += ShowDeck;
        _deckClose.Pressed += () => _deckOverlay.Visible = false;
        _playAgainButton.Pressed += OnPlayAgain;
        _exitButton.Pressed += OnExit;

        Net.GameStateUpdated += OnGameState;
        Net.HandUpdated += OnHand;
        Net.ModifierAssigned += OnModifier;
        Net.EpochDeckUpdated += OnEpochDeck;
        Net.InfoReceived += OnInfo;
        Net.LobbyUpdated += OnLobbyUpdated;
        Net.ErrorReceived += OnError;
        Net.GameOverReceived += OnGameOver;
        Net.ConnectionLost += OnConnectionLost;

        // Poruke koje su stigle dok se scena jos nije ucitala su kesirane
        // u GameNet-u, pa krecemo od njih. Modifikator ide pre ruke, da bi
        // karte odmah bile iscrtane sa modifikovanim vrednostima.
        OnGameState(Net.GameState);

        if (Net.Modifier != null) {
            OnModifier(Net.Modifier);
        }

        if (Net.EpochDeck != null) {
            OnEpochDeck(Net.EpochDeck);
        }

        OnHand(Net.Hand);
    }

    public override void _ExitTree() {
        if (Net == null) {
            return;
        }

        Net.GameStateUpdated -= OnGameState;
        Net.HandUpdated -= OnHand;
        Net.ModifierAssigned -= OnModifier;
        Net.EpochDeckUpdated -= OnEpochDeck;
        Net.InfoReceived -= OnInfo;
        Net.LobbyUpdated -= OnLobbyUpdated;
        Net.ErrorReceived -= OnError;
        Net.GameOverReceived -= OnGameOver;
        Net.ConnectionLost -= OnConnectionLost;
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (_deckOverlay.Visible && @event.IsActionPressed("ui_cancel")) {
            _deckOverlay.Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    // --- traka epoha -----------------------------------------------------

    private void BuildEpochStrip() {
        for (int epoch = 1; epoch <= Ages.EpochCount; epoch++) {
            var cell = new PanelContainer {
                CustomMinimumSize = new Vector2(140, 0),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            cell.AddChild(margin);

            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 2);
            margin.AddChild(box);

            var title = new Label {
                Text = "EPOHA " + epoch,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            title.AddThemeFontSizeOverride("font_size", 11);
            box.AddChild(title);

            box.AddChild(BuildAgeRow(Age.Golden, Ages.GoldenClass(epoch)));
            box.AddChild(BuildAgeRow(Age.Dark, Ages.DarkClass(epoch)));

            if (epoch >= Ages.ModifierEpoch) {
                var note = new Label {
                    Text = "modifikatori",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Modulate = new Color(1f, 1f, 1f, 0.5f),
                };
                note.AddThemeFontSizeOverride("font_size", 10);
                box.AddChild(note);
            }

            _epochCells.Add(cell);
            _epochStrip.AddChild(cell);
        }
    }

    private Label BuildAgeRow(Age age, string factionId) {
        string suffix = factionId == _myFaction ? "  (ti)" : "";

        var label = new Label {
            Text = $"{Ages.Badge(age)} {Factions.DisplayName(factionId)}{suffix}",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", Factions.Tint(factionId));
        label.TooltipText = $"{Ages.DisplayName(age)} - {Ages.Explain(age)}";

        return label;
    }

    private void RefreshEpochStrip(int currentEpoch) {
        for (int i = 0; i < _epochCells.Count; i++) {
            bool isCurrent = i + 1 == currentEpoch;
            _epochCells[i].Modulate = isCurrent
                ? Colors.White
                : new Color(1f, 1f, 1f, 0.38f);
        }
    }

    // --- stanje partije --------------------------------------------------

    private void OnGameState(GameStateData state) {
        int epoch = (int)state.Epoch;

        _epochLabel.Text = $"Epoha {epoch} / {Ages.EpochCount}";
        _turnLabel.Text = $"Runda {state.Turn} od {Ages.EpochCount * 2}";
        _elvesLabel.Text = "Vilenjaci  " + state.Elves;
        _dwarvesLabel.Text = "Patuljci  " + state.Dwarves;
        _humansLabel.Text = "Ljudi  " + state.Humans;

        _factionLabel.Text = Factions.IsValid(_myFaction)
            ? Factions.DisplayName(_myFaction)
            : "-";
        _factionLabel.Modulate = Factions.Tint(_myFaction);

        var age = Ages.Of(_myFaction, epoch);
        _ageLabel.Text = Ages.DisplayName(age);
        _ageLabel.Modulate = Ages.Tint(age);
        _ageLabel.TooltipText = Ages.Explain(age);

        RefreshEpochStrip(epoch);

        // Nova runda znaci da su sva tri igraca odigrala.
        if (state.Turn != _lastTurn) {
            _lastTurn = state.Turn;
            SetWaiting(false);
        }
    }

    private void OnModifier(ModifierData data) {
        // Prazan modifikator stize na pocetku partije i brise onaj iz
        // prethodne, da revans ne bi krenuo sa starim.
        if (data == null || string.IsNullOrEmpty(data.Modifier)) {
            _modifierPanel.Visible = false;
            OnHand(Net.Hand);
            return;
        }

        _modifierPanel.Visible = true;
        _modifierTitle.Text = "Tvoj modifikator: " + Modifiers.DisplayName(data.Modifier);
        _modifierTitle.AddThemeColorOverride("font_color", Modifiers.Tint(data.Modifier));
        _modifierDesc.Text = Modifiers.Explain(data.Modifier, data.Value);

        _log.AppendText($"Dobio si modifikator: {Modifiers.DisplayName(data.Modifier)}. "
            + Modifiers.Explain(data.Modifier, data.Value) + "\n");

        // Vrednosti na kartama se menjaju, pa se ruka iscrtava ponovo.
        OnHand(Net.Hand);
    }

    private void OnEpochDeck(EpochDeckData deck) {
        _deckButton.Disabled = false;
        _deckButton.Text = $"Špil epohe ({deck.Cards.Count})";

        if (_deckOverlay.Visible) {
            ShowDeck();
        }
    }

    private void OnHand(List<string> cardKeys) {
        _playButtons.Clear();

        foreach (var child in _handBox.GetChildren()) {
            _handBox.RemoveChild(child);
            child.QueueFree();
        }

        _handCount = cardKeys != null ? cardKeys.Count : 0;

        if (_handCount > 0) {
            foreach (var key in cardKeys) {
                _handBox.AddChild(BuildCardPanel(key, true));
            }
        }

        SetWaiting(_waitingForOthers);
    }

    private void SetWaiting(bool waiting) {
        _waitingForOthers = waiting;

        foreach (var button in _playButtons) {
            button.Disabled = waiting || _gameOver;
        }

        if (_gameOver) {
            return;
        }

        if (waiting) {
            _hintLabel.Text = "Karta je poslata. Čeka se da odigraju druga dva igrača.";
        } else if (_handCount > 0) {
            _hintLabel.Text = "Odigraj jednu kartu. Potez se razrešava kad odigraju sva tri igrača.";
        } else {
            _hintLabel.Text = "Nema karata u ruci.";
        }
    }

    // --- karte -----------------------------------------------------------

    private Control BuildCardPanel(string key, bool playable) {
        var card = CardDatabase.Get(key);

        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(230, playable ? 300 : 250),
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        var title = new Label {
            Text = card != null ? card.Name : key,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        title.AddThemeFontSizeOverride("font_size", 16);

        if (card != null) {
            title.AddThemeColorOverride("font_color", Factions.Tint(card.Class));
        }

        box.AddChild(title);

        var description = new Label {
            Text = card != null ? card.Description : "(karta nije u lokalnoj bazi)",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Modulate = new Color(1f, 1f, 1f, 0.65f),
        };
        description.AddThemeFontSizeOverride("font_size", 11);
        box.AddChild(description);

        if (card != null) {
            var effects = new VBoxContainer();
            effects.AddThemeConstantOverride("separation", 2);
            effects.AddChild(BuildEffectRow(Factions.Elves, card.Elves));
            effects.AddChild(BuildEffectRow(Factions.Dwarves, card.Dwarves));
            effects.AddChild(BuildEffectRow(Factions.Humans, card.Humans));
            box.AddChild(effects);
        }

        if (playable) {
            var play = new Button { Text = "Odigraj" };
            string cardKey = key;

            play.Pressed += () => {
                Net.PlayCard(cardKey);
                play.Text = "Odigrano ✓";
                MarkPlayed(panel, card);
                SetWaiting(true);
            };

            _playButtons.Add(play);
            box.AddChild(play);
        }

        return panel;
    }

    /// <summary>
    /// Jedan red efekta. Kad je modifikator aktivan, prikazuje se i osnovna
    /// i modifikovana vrednost, da igrac vidi sta mu modifikator radi.
    /// </summary>
    private Control BuildEffectRow(string factionId, int baseValue) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        var name = new Label {
            Text = Factions.DisplayName(factionId),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Modulate = new Color(1f, 1f, 1f, 0.7f),
        };
        name.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(name);

        var modifier = Net.Modifier;
        int shown = modifier != null
            ? Modifiers.Apply(baseValue, modifier.Modifier, modifier.Value)
            : baseValue;

        if (shown != baseValue) {
            var original = new Label {
                Text = Signed(baseValue),
                Modulate = new Color(1f, 1f, 1f, 0.35f),
            };
            original.AddThemeFontSizeOverride("font_size", 12);
            row.AddChild(original);

            var arrow = new Label {
                Text = "→",
                Modulate = new Color(1f, 1f, 1f, 0.35f),
            };
            arrow.AddThemeFontSizeOverride("font_size", 12);
            row.AddChild(arrow);
        }

        var value = new Label { Text = Signed(shown) };
        value.AddThemeFontSizeOverride("font_size", 13);
        value.AddThemeColorOverride("font_color", ValueTint(shown));
        row.AddChild(value);

        return row;
    }

    /// <summary>
    /// Uokviruje kartu koju je igrac upravo odigrao i prigusuje ostale,
    /// da mu bude jasno sta je poslao dok ceka saigrace.
    /// </summary>
    private void MarkPlayed(PanelContainer panel, Card card) {
        var accent = card != null
            ? Factions.Tint(card.Class)
            : new Color(0.55f, 0.75f, 1f);

        var style = new StyleBoxFlat {
            BgColor = new Color(accent.R, accent.G, accent.B, 0.14f),
            BorderColor = accent,
        };
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(6);

        panel.AddThemeStyleboxOverride("panel", style);

        foreach (var child in _handBox.GetChildren()) {
            if (child is Control control && control != panel) {
                control.Modulate = new Color(1f, 1f, 1f, 0.4f);
            }
        }
    }

    private static Color ValueTint(int value) {
        if (value > 0) {
            return new Color(0.45f, 0.85f, 0.5f);
        }

        return value < 0
            ? new Color(0.95f, 0.44f, 0.42f)
            : new Color(0.7f, 0.7f, 0.75f);
    }

    private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

    // --- spil epohe ------------------------------------------------------

    private void ShowDeck() {
        var deck = Net.EpochDeck;

        if (deck == null) {
            return;
        }

        int epoch = (int)deck.Epoch;
        var age = Ages.Of(_myFaction, epoch);

        _deckTitle.Text = $"Špil epohe {epoch} - {Factions.DisplayName(_myFaction)}";
        _deckHint.Text = $"{Ages.DisplayName(age)}: {Ages.Explain(age)}. "
            + $"Svake runde ti se iz ovih {deck.Cards.Count} karata izvlače dve.";
        _deckHint.Modulate = Ages.Tint(age);

        foreach (var child in _deckGrid.GetChildren()) {
            _deckGrid.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var key in deck.Cards) {
            _deckGrid.AddChild(BuildCardPanel(key, false));
        }

        _deckOverlay.Visible = true;
    }

    // --- dogadjaji sa mreze ----------------------------------------------

    private void OnInfo(string text) => _log.AppendText(Escape(text) + "\n");

    private void OnError(string text) =>
        _log.AppendText($"[color=#ff7066]{Escape(text)}[/color]\n");

    /// <summary>
    /// Log je RichTextLabel sa ukljucenim BBCode-om, pa se uglaste zagrade
    /// iz tudjih poruka moraju neutralisati.
    /// </summary>
    private static string Escape(string text) => text.Replace("[", "[lb]");

    private void OnPlayAgain() {
        // Ponistavanje spremnosti tera server da posalje LobbyState svima,
        // pa i ostali igraci padnu nazad u lobi (vidi OnLobbyUpdated).
        Net.SendUnready();
        GetTree().ChangeSceneToFile("res://scenes/Lobby.tscn");
    }

    private void OnExit() {
        Net.LeaveRoom();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    private void OnLobbyUpdated(LobbyStateData _) {
        // Posle kraja partije LobbyState znaci da je neko pokrenuo revans
        // ili napustio sobu; u oba slucaja se vracamo u lobi.
        if (_gameOver) {
            GetTree().ChangeSceneToFile("res://scenes/Lobby.tscn");
        }
    }

    private void OnGameOver(GameOverData data) {
        _gameOver = true;

        foreach (var button in _playButtons) {
            button.Disabled = true;
        }

        _deckOverlay.Visible = false;
        ShowEndScreen(data.Won);
    }

    /// <summary>
    /// Kraj partije ide preko celog ekrana: ishod u prvom planu, konacno
    /// zadovoljstvo sve tri rase, pa dva krupna dugmeta ispod.
    /// </summary>
    private void ShowEndScreen(bool won) {
        var state = Net.GameState;

        if (won) {
            _resultTitle.Text = "POBEDA";
            _resultTitle.Modulate = new Color(0.45f, 0.88f, 0.5f);
            _resultText.Text = "Mir je održan kroz svih šest epoha. "
                + "Nijedna rasa nije izgubila poverenje u svoje saveznike.";
            _hintLabel.Text = "Mir je održan.";
            _log.AppendText("[color=#6ee06e]Pobeda - izdržali ste svih šest epoha.[/color]\n");
        } else {
            _resultTitle.Text = "PORAZ";
            _resultTitle.Modulate = new Color(0.95f, 0.4f, 0.38f);
            _resultText.Text = $"Rat je počeo u {state.Epoch}. epohi. "
                + "Zadovoljstvo jedne rase je palo na nulu i mir nije održan.";
            _hintLabel.Text = "Rat je počeo. Mir nije održan.";
            _log.AppendText("[color=#ff7066]Poraz - zadovoljstvo jedne rase je palo na nulu.[/color]\n");
            _handBox.Modulate = new Color(1f, 1f, 1f, 0.4f);
        }

        foreach (var child in _scoreRow.GetChildren()) {
            _scoreRow.RemoveChild(child);
            child.QueueFree();
        }

        _scoreRow.AddChild(BuildFinalScore(Factions.Elves, state.Elves));
        _scoreRow.AddChild(BuildFinalScore(Factions.Dwarves, state.Dwarves));
        _scoreRow.AddChild(BuildFinalScore(Factions.Humans, state.Humans));

        _endHint.Text = "Igraj ponovo vraća celu ekipu u lobi, gde ponovo birate rase. "
            + "Izađi te vraća na početni ekran.";

        _endOverlay.Visible = true;
        _playAgainButton.GrabFocus();
    }

    private Control BuildFinalScore(string factionId, int value) {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);

        var name = new Label {
            Text = Factions.DisplayName(factionId),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        name.AddThemeFontSizeOverride("font_size", 12);
        name.AddThemeColorOverride("font_color", Factions.Tint(factionId));
        box.AddChild(name);

        var score = new Label {
            Text = value.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        score.AddThemeFontSizeOverride("font_size", 28);
        score.AddThemeColorOverride("font_color", value > 0
            ? new Color(0.9f, 0.9f, 0.94f)
            : new Color(0.95f, 0.4f, 0.38f));
        box.AddChild(score);

        return box;
    }

    private void OnConnectionLost(string reason) {
        _log.AppendText($"[color=#ff7066]Veza je prekinuta: {reason}[/color]\n");
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}
