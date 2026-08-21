namespace Common;
using MessagePack;

using MessagePack.Formatters;


[MessagePackFormatter(typeof(GameMessageFormatter))]
public interface IGameMessage
{
}

[MessagePackObject]
public class ConnectMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "Connect";

    [Key("payload")]
    public ConnectMessageData Data { get; set; } = new();
}

[MessagePackObject]
public class ConnectMessageData {
    [Key("username")]
    public string Content { get; set; } = "";
}


[MessagePackObject]
public class WelcomeMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "Welcome";

    [Key("payload")]
    public WelcomeData Data { get; set; } = new();
}

[MessagePackObject]
public class WelcomeData
{
    [Key("player_id")]
    public uint PlayerId { get; set; }
}

[MessagePackObject]
public class LobbyStateMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "LobbyState";

    [Key("payload")]
    public LobbyStateData Data { get; set; } = new();
}

[MessagePackObject]
public class LobbyStateData
{
    [Key("room_id")]
    public uint RoomId { get; set; }

    [Key("players")]
    public List<LobbyPlayer> Players { get; set; } = new();
}

[MessagePackObject]
public class LobbyPlayer
{
    [Key("id")]
    public uint Id { get; set; }

    [Key("username")]
    public string Username { get; set; } = "";

    [Key("class")]
    public string Class { get; set; } = "";

    [Key("ready")]
    public bool Ready { get; set; }
}

[MessagePackObject]
public class CreateRoomMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "CreateRoom";
}

[MessagePackObject]
public class LeaveRoomMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "LeaveRoom";
}

[MessagePackObject]
public class JoinRoomMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "JoinRoom";

    [Key("payload")]
    public JoinRoomData Data { get; set; } = new();
}

[MessagePackObject]
public class JoinRoomData {
    [Key("room_id")]
    public int RoomId { get; set; }
}

[MessagePackObject]
public class ChatMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "Chat";

    [Key("payload")]
    public ChatData Data { get; set; } = new();
}

[MessagePackObject]
public class ChatData
{
    [Key("content")]
    public string Content { get; set; } = "";
}

[MessagePackObject]
public class ReadyMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "Ready";

    [Key("payload")]
    public ReadyData Data { get; set; } = new();
}

[MessagePackObject]
public class ReadyData
{
    [Key("class")]
    public string Class { get; set; } = "";
}

[MessagePackObject]
public class UnreadyMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "Unready";
}

[MessagePackObject]
public class GameOverMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "GameOver";
}

[MessagePackObject]
public class CardMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "Card";

    [Key("payload")]
    public CardData Data { get; set; } = new();
}

[MessagePackObject]
public class CardData
{
    [Key("name")]
    public string Name { get; set; } = "";
}

[MessagePackObject]
public class HandMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "Hand";

    [Key("payload")]
    public HandData Data { get; set; } = new();
}

[MessagePackObject]
public class HandData
{
    [Key("cards")]
    public List<string> Cards { get; set; } = new();
}

[MessagePackObject]
public class StartGameMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "StartGame";
    
    [Key("payload")]
    public StartGameData Data { get; set; } = new();
}

[MessagePackObject]
public class StartGameData
{
    [Key("seed")]
    public ulong Seed { get; set; }
}

[MessagePackObject]
public class GameStateMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "GameState";

    [Key("payload")]
    public GameStateData Data { get; set; } = new();
}

[MessagePackObject]
public class GameStateData
{
    [Key("seed")]
    public ulong Seed { get; set; } = 0;

    [Key("turn")]
    public uint Turn { get; set; } = 0;

    [Key("epoch")]
    public uint Epoch { get; set; } = 0;

    [Key("elves")]
    public int Elves { get; set; } = 0;

    [Key("dwarves")]
    public int Dwarves { get; set; } = 0;

    [Key("humans")]
    public int Humans { get; set; } = 0;
}

[MessagePackObject]
public class ModifierMessage : IGameMessage{
    [Key("name")]
    public string Name { get; set; } = "";

    [Key("payload")]
    public ModifierData Data { get; set; } = new();
}

[MessagePackObject]
public class ModifierData {
    [Key("modifier")]
    public string Modifier { get; set; } = "";
    [Key("value")]
    public float Value { get; set; } = 0;
}

[MessagePackObject]
public class ResponseMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "Response";

    [Key("payload")]
    public ResponseData Data { get; set; } = new();
}

[MessagePackObject]
public class ResponseData
{
    [Key("content")]
    public string Content { get; set; } = "";
}

[MessagePackObject]
public class ErrorMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "Error";

    [Key("payload")]
    public ErrorData Data { get; set; } = new();
}

[MessagePackObject]
public class ErrorData
{
    [Key("message")]
    public string Message { get; set; } = "";
}


public class GameMessageFormatter : IMessagePackFormatter<IGameMessage>
{
    public void Serialize(ref MessagePackWriter writer, IGameMessage value, MessagePackSerializerOptions options)
    {
        // Serialize based on the concrete implementation type (e.g. MoveMessage, ChatMessage)
        MessagePackSerializer.Serialize(value.GetType(), ref writer, value, options);
    }

    public IGameMessage Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        // Create a copy of the reader to peek at the map fields without advancing the original reader
        var peekReader = reader;
        
        int mapCount = peekReader.ReadMapHeader();
        string kind = null;

        for (int i = 0; i < mapCount; i++)
        {
            string key = peekReader.ReadString();
            if (key == "kind")
            {
                kind = peekReader.ReadString();
                break;
            }
            else
            {
                // Skip the value of the unknown or irrelevant property
                peekReader.Skip();
            }
        }

        // Now use the original reader to deserialize the full map into the matched class
        return kind switch
        {
            "Connect" => MessagePackSerializer.Deserialize<ConnectMessage>(ref reader, options),
            "Welcome" => MessagePackSerializer.Deserialize<WelcomeMessage>(ref reader, options),
            "CreateRoom" => MessagePackSerializer.Deserialize<CreateRoomMessage>(ref reader, options),
            "LobbyState" => MessagePackSerializer.Deserialize<LobbyStateMessage>(ref reader, options),
            "Ready" => MessagePackSerializer.Deserialize<ReadyMessage>(ref reader, options),
            "Unready" => MessagePackSerializer.Deserialize<UnreadyMessage>(ref reader, options),
            "GameOver" => MessagePackSerializer.Deserialize<GameOverMessage>(ref reader, options),
            "LeaveRoom" => MessagePackSerializer.Deserialize<LeaveRoomMessage>(ref reader, options),
            "JoinRoom" => MessagePackSerializer.Deserialize<JoinRoomMessage>(ref reader, options),
            "Card" => MessagePackSerializer.Deserialize<CardMessage>(ref reader, options),
            "Hand" => MessagePackSerializer.Deserialize<HandMessage>(ref reader, options),
            "StartGame" => MessagePackSerializer.Deserialize<StartGameMessage>(ref reader, options),
            "GameState" => MessagePackSerializer.Deserialize<GameStateMessage>(ref reader, options),
            "Chat" => MessagePackSerializer.Deserialize<ChatMessage>(ref reader, options),
            "Response" => MessagePackSerializer.Deserialize<ResponseMessage>(ref reader, options),
            "Error" => MessagePackSerializer.Deserialize<ErrorMessage>(ref reader, options),
            _ => throw new Exception($"Unknown message kind: {kind}")
        };
    }
}
