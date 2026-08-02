using Godot;
using System;
using System.Net;
using Common;

public partial class MainMenu : Control {
    private Label _statusLabel;

    public override void _Ready() {
        _statusLabel = GetNode<Label>("StatusLabel");
        _statusLabel.Text = "Status: nije povezan";

        GD.Print("Main Menu radi");
    }

    public async void _on_button_pressed() {
        _statusLabel.Text = "Povezivanje";

        try {
            Communication.Instance.Connect("127.0.0.1", 8080);

            _statusLabel.Text = "Uspesno povezano";

        } catch (Exception ex) {
            _statusLabel.Text = "Server vratio gresku:" + ex.ToString();
        }
        
    }
}
