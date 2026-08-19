using System.Threading.Tasks;
using Godot;
using System;
using Common;

public partial class MainMenu : Control {
    private Label _statusLabel;
    private Button _connectButton;
    private Label _nametag;

    public override void _Ready() {
        _statusLabel = GetNode<Label>("StatusLabel");
        _connectButton = GetNode<Button>("ConnectButton");
        _nametag = GetNode<Label>("Nametag");
        _statusLabel.Text = "Status: nije povezan";
    }

    private async Task ListenToServer() {
        while (true) {
            try {
                var message = await Communication.Instance.ReceiveMessageAsync();

                if (message is ResponseMessage responseMessage) {
                    CallDeferred(nameof(UpdateText), responseMessage.Data.Content);
                } else if (message is ErrorMessage errorMessage) {
                    CallDeferred(nameof(UpdateText), "Greska: " + errorMessage.Data.Message);
                }
            } catch (Exception ex) {
                CallDeferred(nameof(UpdateText), "Server vratio gresku:" + ex.ToString());
            }
        }
    }

    private void UpdateText(string text) {
        GD.Print("Update text called with: " + text);
        _statusLabel.Text = text;
    }

    public async void _on_connect_button_pressed() {
        _statusLabel.Text = "Povezivanje";

        try {
            Communication.Instance.Connect("127.0.0.1", 8080);
            _connectButton.Disabled = true;
            var txtUsername = GetNode<TextEdit>("TxtUsername");
            txtUsername.Editable = false;
            Communication.Instance.SendMessage(new ConnectMessage {
                    Data = new ConnectMessageData {
                        Content = txtUsername.Text } });
            _nametag.Text = "Username: " + txtUsername.Text;


            await Task.Run(ListenToServer);

        } catch (Exception ex) {
            _statusLabel.Text = "Server vratio gresku:" + ex.ToString();
        }
    }

    public async void _on_create_room_button_pressed() {
        try {
            Communication.Instance.SendMessage(new CreateRoomMessage());

            // var response = Communication.Instance.ReceiveMessage();
            //
            // if (!(response is ErrorMessage)) {
            //     var message = response as ResponseMessage;
            //
            //     _statusLabel.Text = $"Povezan: {message.Data.Content}";
            // } else {
            //     var errorMessage = response as ErrorMessage;
            //
            //     _statusLabel.Text = $"Greska: {errorMessage.Data.Message}";
            // }
            //
        } catch (Exception ex) {
            _statusLabel.Text = "Server vratio gresku:" + ex.ToString();
        }
    }

    public async void _on_join_room_button_pressed() {
        if (int.TryParse(GetNode<TextEdit>("TxtRoomId").Text, out int roomId)) {
            try {
                Communication.Instance.SendMessage(new JoinRoomMessage { Data = new JoinRoomData { RoomId = roomId } });

                // var response = Communication.Instance.ReceiveMessage();

                // if (!(response is ErrorMessage)) {
                //     var message = response as ResponseMessage;
                //
                //     _statusLabel.Text = $"Povezan: {message.Data.Content}";
                // } else {
                //     var errorMessage = response as ErrorMessage;
                //
                //     _statusLabel.Text = $"Greska: {errorMessage.Data.Message}";
                // }

            } catch (Exception ex) {
                _statusLabel.Text = "Server vratio gresku:" + ex.ToString();
            }
        } else {
            _statusLabel.Text = "Greska: Nevalidan ID sobe";
        }
    }

    public async void _on_leave_room_button_pressed() {
        try {
            Communication.Instance.SendMessage(new LeaveRoomMessage());

            // var response = Communication.Instance.ReceiveMessage();
            //
            // if (!(response is ErrorMessage)) {
            //     var message = response as ResponseMessage;
            //
            //     _statusLabel.Text = $"Povezan: {message.Data.Content}";
            // } else {
            //     var errorMessage = response as ErrorMessage;
            //
            //     _statusLabel.Text = $"Greska: {errorMessage.Data.Message}";
            // }

        } catch (Exception ex) {
            _statusLabel.Text = "Server vratio gresku:" + ex.ToString();
        }
    }

}
