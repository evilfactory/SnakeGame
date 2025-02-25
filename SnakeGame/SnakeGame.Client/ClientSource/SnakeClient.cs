using MalignEngine;
using System.Net;
using TcpTransport = SnakeGame.TcpTransport;

namespace SnakeGame;

public class SnakeClient : EntitySystem
{
    [Dependency]
    protected SnakeRendering SnakeRendering = default!;
    [Dependency]
    protected MainMenu MainMenu = default!;
    [Dependency]
    protected InputSystem InputSystem = default!;

    public Transport Transport { get; private set; }

    private GameConfig gameConfig = new GameConfig() { TickFrequency = 20 };

    private DateTime lastNetworkUpdateTime = DateTime.Now;

    private byte myClient;
    private Board board;

    protected ILogger logger;

    private PacketSerializer packetSerializer = new PacketSerializer();

    private bool connected = false;

    private bool inputChanged = false;
    private PlayerInput lastPlayerInput;

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

        if (!connected)
        {
            return;
        }

        if (InputSystem.IsKeyDown(Key.W))
        {
            lastPlayerInput = PlayerInput.Up;
            inputChanged = true;
        }
        else if (InputSystem.IsKeyDown(Key.S))
        {
            inputChanged = true;
            lastPlayerInput = PlayerInput.Down;
        }
        else if (InputSystem.IsKeyDown(Key.A))
        {
            inputChanged = true;
            lastPlayerInput = PlayerInput.Left;
        }
        else if (InputSystem.IsKeyDown(Key.D))
        {
            inputChanged = true;
            lastPlayerInput = PlayerInput.Right;
        }

        // Only send a message every gameConfig.TickFrequency
        if (DateTime.Now - lastNetworkUpdateTime > TimeSpan.FromSeconds(1.0 / gameConfig.TickFrequency))
        {
            if (inputChanged)
            {
                IWriteMessage inputMessage = new WriteOnlyMessage();
                inputMessage.WriteByte((byte)lastPlayerInput);
                SendToServer(ClientToServer.PlayerInput, inputMessage);

                inputChanged = false;
            }

            IWriteMessage message = new WriteOnlyMessage();
            if (packetSerializer.BuildMessage(message, logger))
            {
                Transport.SendToServer(message);
            }

            lastNetworkUpdateTime = DateTime.Now;
        }
    }

    public override void OnDraw(float deltaTime)
    {
        if (board != null)
        {
            SnakeRendering.DrawBoard(board, myClient);
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
        MainMenu.ShowMainMenu = false;

        SendToServer(ClientToServer.RequestLobbyInfo, new WriteOnlyMessage());

        ConnectingNetMessage connecting = new ConnectingNetMessage()
        {
            HostInfo = new HostInfo()
            {
                AgentString = "Evil Snake Client",
                VersionMajor = 0,
                VersionMinor = 11
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
        MainMenu.ShowMainMenu = true;
    }

    public void OnMessageReceived(IReadMessage incomingMessage)
    {
        //logger.LogInfo($"Message received from server");
        //logger.LogVerbose(string.Join(" ", incomingMessage.Buffer.Select(b => b.ToString("X2"))));

        packetSerializer.ReadIncoming(incomingMessage, (ServerToClient messageType, IReadMessage message) =>
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
                case ServerToClient.PlayerRenamed:
                    HandlePlayerRenamed(message);
                    break;
            }
        }, logger);
    }

    private void HandleLobbyInformation(IReadMessage message)
    {
        LobbyInformation lobbyInformation = new LobbyInformation();
        lobbyInformation.Deserialize(message);

        logger.LogInfo($"Received lobby information: {lobbyInformation}");
    }

    private void HandleGameConfig(IReadMessage message)
    {
        gameConfig = new GameConfig();
        gameConfig.Deserialize(message);
        logger.LogInfo($"Received game config: {gameConfig}");
    }

    private void HandleAssignPlayerId(IReadMessage message)
    {
        AssignPlayerId assignPlayerId = new AssignPlayerId();
        assignPlayerId.Deserialize(message);

        myClient = assignPlayerId.PlayerId;

        logger.LogInfo($"Received player id: {assignPlayerId}");
    }

    private void HandleBoardReset(IReadMessage message)
    {
        BoardResetNetMessage boardReset = new BoardResetNetMessage();
        boardReset.Deserialize(message);

        board = new Board(boardReset.Width, boardReset.Height);

        logger.LogInfo($"Received board reset: {boardReset}");
    }

    private void HandleBoardSet(IReadMessage message)
    {
        BoardSet boardSet = new BoardSet();
        boardSet.Deserialize(message);

        board.SetResource(boardSet.X, boardSet.Y, new Tile()
        {
            PlayerId = boardSet.Data.AssociatedPlayerId,
            Type = boardSet.Data.Resource
        });

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
        PlayerSpawnedNetMessage playerSpawned = new PlayerSpawnedNetMessage();
        playerSpawned.Deserialize(message);

        logger.LogInfo($"Received player spawned: {playerSpawned}");
    }

    private void HandlePlayerDied(IReadMessage message)
    {
        PlayerDiedNetMessage playerDied = new PlayerDiedNetMessage();
        playerDied.Deserialize(message);

        logger.LogInfo($"Received player died: {playerDied}");
    }

    private void HandlePlayerMoved(IReadMessage message)
    {
        PlayerMovedNetMessage playerMoved = new PlayerMovedNetMessage();
        playerMoved.Deserialize(message);

        logger.LogInfo($"Received player moved: {playerMoved}");
    }

    private void HandleRespawnAllowed(IReadMessage message)
    {
        IWriteMessage playerInput = new WriteOnlyMessage();
        playerInput.WriteByte((byte)PlayerInput.Respawn);
        SendToServer(ClientToServer.PlayerInput, playerInput);

        logger.LogInfo($"Received respawn allowed");
    }

    private void HandlePlayerRenamed(IReadMessage message)
    {
        PlayerRenamed playerRenamed = new PlayerRenamed();
        playerRenamed.Deserialize(message);
        logger.LogInfo($"Received player renamed: {playerRenamed}");
    }
}
