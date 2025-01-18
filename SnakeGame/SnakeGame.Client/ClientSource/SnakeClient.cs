using MalignEngine;
using System.Net;

namespace SnakeGame;

public class SnakeClient : EntitySystem
{
    public Transport Transport { get; private set; }

    protected ILogger logger;

    private PacketSerializer packetSerializer = new PacketSerializer();
    private PacketDeserializer packetDeserializer = new PacketDeserializer();

    private bool connected = false;

    public override void OnInitialize()
    {
        logger = LoggerService.GetSawmill("snake-client");

        Transport = new TcpTransport();
        Transport.Logger = logger;

        Transport.OnConnected = OnConnected;
        Transport.OnDisconnected = OnDisconnected;
        Transport.OnMessageReceived = OnMessageReceived;
    }

    public override void OnUpdate(float deltaTime)
    {
        Transport.Update();

        if (connected)
        {
            IWriteMessage message = new WriteOnlyMessage();
            if (packetSerializer.BuildMessage(message))
            {
                Transport.SendToServer(message);
            }
        }
    }

    public void Connect(IPEndPoint endpoint)
    {
        Transport.Connect(endpoint);
    }

    public void Disconnect()
    {
        Transport.Disconnect(DisconnectReason.Unknown);
    }

    public void SendToServer(ClientToServer messageType, IWriteMessage message)
    {
        packetSerializer.QueueMessage(message, messageType);
    }

    public void OnConnected()
    {
        logger.LogInfo("Connected to server");

        connected = true;

        SendToServer(ClientToServer.RequestLobbyInfo, new WriteOnlyMessage());

        Connecting connecting = new Connecting()
        {
            Name = "Evil",
            HostInfo = new HostInfo()
            {
                AgentString = "Evil Snake Client",
                VersionMajor = 0,
                VersionMinor = 8
            }
        };

        IWriteMessage message = new WriteOnlyMessage();
        connecting.Serialize(message);
        SendToServer(ClientToServer.Connecting, message);
    }

    public void OnDisconnected(DisconnectReason reason)
    {
        logger.LogInfo("Disconnected from server");

        connected = false;
    }

    public void OnMessageReceived(IReadMessage incomingMessage)
    {
        logger.LogInfo($"Message received from server");
        logger.LogVerbose(string.Join(" ", incomingMessage.Buffer.Select(b => b.ToString("X2"))));

        var result = packetDeserializer.ReadIncoming(incomingMessage, (ServerToClient messageType, IReadMessage message) =>
        {
            switch (messageType)
            {
                case ServerToClient.LobbyInformation:
                    HandleLobbyInformation(message);
                    break;
                case ServerToClient.GameConfig:
                    HandleGameConfig(message);
                    break;
                case ServerToClient.AssignPlayerId:
                    HandleAssignPlayerId(message);
                    break;
                case ServerToClient.PlayerConnected:
                    HandlePlayerConnected(message);
                    break;
            }
        });

        foreach (var error in result.Errors)
        {
            logger.LogError(error.Message);
        }
    }

    private void HandleLobbyInformation(IReadMessage message)
    {
        LobbyInformation lobbyInformation = new LobbyInformation();
        lobbyInformation.Deserialize(message);

        logger.LogInfo($"Received lobby information: {lobbyInformation}");
    }

    private void HandleGameConfig(IReadMessage message)
    {
        GameConfig gameConfig = new GameConfig();
        gameConfig.Deserialize(message);

        logger.LogInfo($"Received game config: {gameConfig}");
    }

    private void HandleAssignPlayerId(IReadMessage message)
    {
        AssignPlayerId assignPlayerId = new AssignPlayerId();
        assignPlayerId.Deserialize(message);

        logger.LogInfo($"Received player id: {assignPlayerId}");
    }

    private void HandlePlayerConnected(IReadMessage message)
    {
        PlayerConnected playerConnected = new PlayerConnected();
        playerConnected.Deserialize(message);

        logger.LogInfo($"Received player connected: {playerConnected}");
    }
}
