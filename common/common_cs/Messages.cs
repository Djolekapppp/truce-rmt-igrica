namespace Common;
using MessagePack;

using MessagePack.Formatters;

[MessagePackFormatter(typeof(GameMessageFormatter))]
public interface IGameMessage
{
}

[MessagePackObject]
public class MoveMessage : IGameMessage
{
    [Key("kind")]
    public string Type => "Move";

    [Key("payload")]
    public MoveData Data { get; set; } = new();
}

[MessagePackObject]
public class MoveData
{
    [Key("x")]
    public int X { get; set; }

    [Key("y")]
    public int Y { get; set; }
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
            "Move" => MessagePackSerializer.Deserialize<MoveMessage>(ref reader, options),
            "Chat" => MessagePackSerializer.Deserialize<ChatMessage>(ref reader, options),
            "Response" => MessagePackSerializer.Deserialize<ResponseMessage>(ref reader, options),
            _ => throw new Exception($"Unknown message kind: {kind}")
        };
    }
}
