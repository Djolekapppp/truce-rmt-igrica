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
            "CreateRoom" => MessagePackSerializer.Deserialize<CreateRoomMessage>(ref reader, options),
            "LeaveRoom" => MessagePackSerializer.Deserialize<LeaveRoomMessage>(ref reader, options),
            "JoinRoom" => MessagePackSerializer.Deserialize<JoinRoomMessage>(ref reader, options),
            "Chat" => MessagePackSerializer.Deserialize<ChatMessage>(ref reader, options),
            "Response" => MessagePackSerializer.Deserialize<ResponseMessage>(ref reader, options),
            "Error" => MessagePackSerializer.Deserialize<ErrorMessage>(ref reader, options),
            _ => throw new Exception($"Unknown message kind: {kind}")
        };
    }
}
