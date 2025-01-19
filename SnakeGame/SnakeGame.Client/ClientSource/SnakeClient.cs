using MalignEngine;
using System.Net;

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

        if (!connected)
        {
            return;
        }

        PlayerInput playerInput = PlayerInput.Up;
        bool inputReceived = false;

        if (InputSystem.IsKeyDown(Key.W))
        {
            playerInput = PlayerInput.Up;
            inputReceived = true;
        }
        else if (InputSystem.IsKeyDown(Key.S))
        {
            playerInput = PlayerInput.Down;
            inputReceived = true;
        }
        else if (InputSystem.IsKeyDown(Key.A))
        {
            playerInput = PlayerInput.Left;
            inputReceived = true;
        }
        else if (InputSystem.IsKeyDown(Key.D))
        {
            playerInput = PlayerInput.Right;
            inputReceived = true;
        }

        // Only send a message every gameConfig.TickFrequency
        if ((DateTime.Now - lastNetworkUpdateTime).TotalMilliseconds > gameConfig.TickFrequency)
        {
            if (inputReceived)
            {
                IWriteMessage inputMessage = new WriteOnlyMessage();
                inputMessage.WriteByte((byte)playerInput);
                SendToServer(ClientToServer.PlayerInput, inputMessage);
            }

            IWriteMessage message = new WriteOnlyMessage();
            if (packetSerializer.BuildMessage(message))
            {
                Transport.SendToServer(message);
            }
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
        MainMenu.ShowMainMenu = true;
    }

    public void OnMessageReceived(IReadMessage incomingMessage)
    {
        //logger.LogInfo($"Message received from server");
        //logger.LogVerbose(string.Join(" ", incomingMessage.Buffer.Select(b => b.ToString("X2"))));

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
        BoardReset boardReset = new BoardReset();
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
        IWriteMessage playerInput = new WriteOnlyMessage();
        playerInput.WriteByte((byte)PlayerInput.Respawn);
        SendToServer(ClientToServer.PlayerInput, playerInput);

        logger.LogInfo($"Received respawn allowed");
    }
}
