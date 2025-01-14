using MalignEngine;

namespace SnakeGame;

public class SnakeServer : EntitySystem
{
    public Transport Transport { get; private set; }

    private List<NetworkConnection> clients = new List<NetworkConnection>();

    protected ILogger logger;

    public override void OnInitialize()
    {
        logger = LoggerService.GetSawmill("snake-server");

        Transport = new TcpTransport();
        Transport.Logger = logger;

        Transport.OnClientConnected = OnClientConnected;
        Transport.OnClientDisconnected = OnClientDisconnected;
        Transport.OnMessageReceived = OnMessageReceived;
    }

    public void Listen(int port)
    {
        Transport.Listen(port);

        logger.LogInfo($"Listening on port {port}");
    }

    public void Shutdown()
    {
        Transport.Shutdown();
    }

    public void SendToClient(IWriteMessage message, NetworkConnection connection, PacketChannel packetChannel = PacketChannel.Reliable)
    {
        Transport.SendToClient(message, connection, packetChannel);
    }

    public void OnClientConnected(NetworkConnection connection)
    {
        logger.LogInfo($"Client connected: {connection.Id}");
    }

    public void OnClientDisconnected(NetworkConnection connection, DisconnectReason reason)
    {
        logger.LogInfo($"Client disconnected: {connection.Id}");

        clients.Remove(connection);
    }

    public void OnMessageReceived(IReadMessage message)
    {
        logger.LogInfo($"Message received from {message.Sender.Id}");

        byte clientTick = message.ReadByte();
        byte messageCount = message.ReadByte();

        for (int i = 0; i < messageCount; i++)
        {
            ClientToServer messageType = (ClientToServer)message.ReadByte();

            switch (messageType)
            {
                case ClientToServer.RequestLobbyInfo:
                    HandleConnecting(message);
                    break;
                case ClientToServer.Connecting:
                    break;
            }
        }
    }

    public void SendMessageToClient(NetworkConnection connection, ServerToClient messageType, IWriteMessage message)
    {
        message.WriteByte(0);
        message.WriteByte(1);
        message.WriteByte((byte)messageType);
        SendToClient(message, connection);
    }

    private void HandleRequestLobbyInfo(IReadMessage message)
    {
        WriteOnlyMessage response = new WriteOnlyMessage();

        response.WriteByte((byte)clients.Count);
        response.WriteString("拼图是毛茸茸的");
        response.WriteString("这是真的");

        HostInfo hostInfo = new HostInfo
        {
            VersionMajor = 9,
            VersionMinor = 0,
            AgentString = "Evil Snake Server"
        };

        hostInfo.Serialize(response);

        SendMessageToClient(message.Sender, ServerToClient.LobbyInformation, response);
    }

    private void HandleConnecting(IReadMessage message)
    {
        if (clients.Contains(message.Sender))
        {
            return;
        }

        clients.Add(message.Sender);

        string name = message.ReadString();
        HostInfo hostInfo = new HostInfo();
        hostInfo.Deserialize(message);

        logger.LogInfo($"Client {message.Sender.Id} connected with name {name} and agent {hostInfo.AgentString}, snake {hostInfo.VersionMajor}.{hostInfo.VersionMinor}");
    }
}
