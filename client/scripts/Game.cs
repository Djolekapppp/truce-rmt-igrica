using System.Collections.Generic;
using Common;
using Godot;

/// <summary>
/// Ekran partije. Za sada minimalan: prikazuje stanje koje server salje i
/// dozvoljava odigravanje jedne karte iz ruke.
/// </summary>
public partial class Game : Control {
    private Label _epochLabel;
    private Label _turnLabel;
    private Label _factionLabel;
    private Label _elfLabel;
    private Label _dwarfLabel;
    private Label _humanLabel;
    private Label _hintLabel;
    private HBoxContainer _handBox;
    private RichTextLabel _log;

    private string _myFaction = "";

    private GameNet Net => GameNet.Instance;

    public override void _Ready() {
        _epochLabel = GetNode<Label>("%EpochLabel");
        _turnLabel = GetNode<Label>("%TurnLabel");
        _factionLabel = GetNode<Label>("%FactionLabel");
        _elfLabel = GetNode<Label>("%NatureLabel");
        _dwarfLabel = GetNode<Label>("%FaithLabel");
        _humanLabel = GetNode<Label>("%ScienceLabel");
        _hintLabel = GetNode<Label>("%HintLabel");
        _handBox = GetNode<HBoxContainer>("%HandBox");
        _log = GetNode<RichTextLabel>("%Log");

        var me = Net.FindMe();
        _myFaction = me != null ? me.Class : "";

        Net.GameStateUpdated += OnGameState;
        Net.HandUpdated += OnHand;
        Net.InfoReceived += OnInfo;
        Net.ChatReceived += OnInfo;
        Net.ErrorReceived += OnError;
        Net.GameOverReceived += OnGameOver;
        Net.ConnectionLost += OnConnectionLost;

        // Poruke koje su stigle dok se scena jos nije ucitala su kesirane
        // u GameNet-u, pa krecemo od njih.
        OnGameState(Net.GameState);
        OnHand(Net.Hand);
    }

    public override void _ExitTree() {
        if (Net == null) {
            return;
        }

        Net.GameStateUpdated -= OnGameState;
        Net.HandUpdated -= OnHand;
        Net.InfoReceived -= OnInfo;
        Net.ChatReceived -= OnInfo;
        Net.ErrorReceived -= OnError;
        Net.GameOverReceived -= OnGameOver;
        Net.ConnectionLost -= OnConnectionLost;
    }

    private void OnGameState(GameStateData state) {
        _epochLabel.Text = "Epoha " + state.Epoch;
        _turnLabel.Text = "Potez " + state.Turn;
        _elfLabel.Text = "Vilenjaci  " + state.Elves;
        _dwarfLabel.Text = "Patuljci  " + state.Dwarves;
        _humanLabel.Text = "Ljudi  " + state.Humans;

        _factionLabel.Text = Factions.IsValid(_myFaction)
            ? Factions.DisplayName(_myFaction)
            : "-";
        _factionLabel.Modulate = Factions.Tint(_myFaction);
    }

    private void OnHand(List<string> cardKeys) {
        foreach (var child in _handBox.GetChildren()) {
            _handBox.RemoveChild(child);
            child.QueueFree();
        }

        if (cardKeys == null || cardKeys.Count == 0) {
            _hintLabel.Text = "Nema karata u ruci.";
            return;
        }

        _hintLabel.Text = "Odigraj jednu kartu.";

        foreach (var key in cardKeys) {
            _handBox.AddChild(BuildCard(key));
        }
    }

    private Control BuildCard(string key) {
        var card = CardDatabase.Get(key);

        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(200, 260),
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
        title.AddThemeFontSizeOverride("font_size", 18);

        if (card != null) {
            title.AddThemeColorOverride("font_color", Factions.Tint(card.Class));
        }

        box.AddChild(title);

        var description = new Label {
            Text = card != null ? card.Description : "(karta nije u lokalnoj bazi)",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Modulate = new Color(1f, 1f, 1f, 0.7f),
        };
        description.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(description);

        if (card != null) {
            box.AddChild(new Label {
                Text = $"Vilenjaci {Signed(card.Elves)}   Patuljci {Signed(card.Dwarves)}   Ljudi {Signed(card.Humans)}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
        }

        var play = new Button { Text = "Odigraj" };
        string cardKey = key;
        play.Pressed += () => Net.PlayCard(cardKey);
        box.AddChild(play);

        return panel;
    }

    private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

    private void OnInfo(string text) => _log.AppendText(text + "\n");

    private void OnError(string text) => _log.AppendText($"[color=#ff7066]{text}[/color]\n");

    private void OnGameOver() {
        _hintLabel.Text = "Rat je poceo. Mir nije odrzan.";
        _log.AppendText("[color=#ff7066]Kraj partije - izgubili ste.[/color]\n");

        foreach (var child in _handBox.GetChildren()) {
            if (child is Control control) {
                control.Modulate = new Color(1f, 1f, 1f, 0.4f);
            }
        }
    }

    private void OnConnectionLost(string reason) {
        _log.AppendText($"[color=#ff7066]Veza je prekinuta: {reason}[/color]\n");
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}
