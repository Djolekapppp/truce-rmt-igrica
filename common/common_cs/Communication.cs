namespace Common;

using System.Net.Sockets;

public class Communication {
    private static Communication instance;

    private Communication() {}

    public static Communication Instance {
        get {
            if (instance == null) {
                instance = new Communication();
            }
            return instance;
        }
    }

    private Socket socket;
    private NetworkStream stream;

    public void Connect(string host, int port) {
        socket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(host, port);

        stream = new NetworkStream(socket);
    }

    public IGameMessage RecieveMessage() {
        byte[] buffer = new byte[4];

        try {
            stream.ReadExactly(buffer, 0, buffer.Length);

            int len = BitConverter.ToInt32(buffer, 0);

            Console.WriteLine($"Received message length: {len}");

            byte[] messageBuffer = new byte[len];

            stream.ReadExactly(messageBuffer, 0, messageBuffer.Length);

            var message = MessagePack.MessagePackSerializer
                .Deserialize<IGameMessage>(messageBuffer);

            return message;
        }
        catch (Exception ex) {
            throw new Exception($"Error receiving message: {ex.Message}");
        }
    }

    public void SendMessage(IGameMessage message) {
        byte[] messageBuffer = MessagePack.MessagePackSerializer
            .Serialize(message);

        byte[] lengthBuffer = BitConverter.GetBytes(messageBuffer.Length);

        try {
            stream.Write(lengthBuffer, 0, lengthBuffer.Length);
            stream.Write(messageBuffer, 0, messageBuffer.Length);
        } catch (Exception ex) {
            throw new Exception($"Error sending message: {ex.Message}");
        }
    }


}
