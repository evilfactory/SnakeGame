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

        SendToServer(ClientToServer.FullUpdate, new WriteOnlyMessage());
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
                case ServerToClient.BoardReset:
                    HandleBoardReset(message);
                    break;
                case ServerToClient.BoardSet:
                    HandleBoardSet(message);
                    break;
                case ServerToClient.PlayerConnected:
                    HandlePlayerConnected(message);
                    break;
                case ServerToClient.PlayerDisconnected:
                    HandlePlayerDisconnected(message);
                    break;
                case ServerToClient.PlayerSpawned:
                    HandlePlayerSpawned(message);
                    break;
                case ServerToClient.PlayerDied:
                    HandlePlayerDied(message);
                    break;
                case ServerToClient.PlayerMoved:
                    HandlePlayerMoved(message);
                    break;
                case ServerToClient.RespawnAllowed:
                    HandleRespawnAllowed(message);
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

    private void HandleBoardReset(IReadMessage message)
    {
        BoardReset boardReset = new BoardReset();
        boardReset.Deserialize(message);

        logger.LogInfo($"Received board reset: {boardReset}");
    }

    private void HandleBoardSet(IReadMessage message)
    {
        BoardSet boardSet = new BoardSet();
        boardSet.Deserialize(message);

        logger.LogInfo($"Received board set: {boardSet}");
    }

    private void HandlePlayerConnected(IReadMessage message)
    {
        PlayerConnected playerConnected = new PlayerConnected();
        playerConnected.Deserialize(message);

        logger.LogInfo($"Received player connected: {playerConnected}");
    }

    private void HandlePlayerDisconnected(IReadMessage message)
    {
        PlayerDisconnected playerDisconnected = new PlayerDisconnected();
        playerDisconnected.Deserialize(message);

        logger.LogInfo($"Received player disconnected: {playerDisconnected}");
    }

    private void HandlePlayerSpawned(IReadMessage message)
    {
        PlayerSpawned playerSpawned = new PlayerSpawned();
        playerSpawned.Deserialize(message);

        logger.LogInfo($"Received player spawned: {playerSpawned}");
    }

    private void HandlePlayerDied(IReadMessage message)
    {
        PlayerDied playerDied = new PlayerDied();
        playerDied.Deserialize(message);

        logger.LogInfo($"Received player died: {playerDied}");
    }

    private void HandlePlayerMoved(IReadMessage message)
    {
        PlayerMoved playerMoved = new PlayerMoved();
        playerMoved.Deserialize(message);

        logger.LogInfo($"Received player moved: {playerMoved}");
    }

    private void HandleRespawnAllowed(IReadMessage message)
    {
        SendToServer(ClientToServer.RequestRespawn, new WriteOnlyMessage());

        logger.LogInfo($"Received respawn allowed");
    }
}
