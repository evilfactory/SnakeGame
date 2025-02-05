using MalignEngine;
using Silk.NET.Maths;
using System.Data.Common;
using TcpTransport = SnakeGame.TcpTransport;

namespace SnakeGame;

public interface IReceiveClientInput : IEvent
{
    void ReceiveInput(Client client, PlayerInput input);
}

public class SnakeServer : EntitySystem
{
    private class QueuedSendMessage
    {
        public IWriteMessage Message { get; set; }
    }

    public GameMode GameMode { get; private set; }
    public Simulation Simulation { get; private set; }

    public Transport Transport { get; private set; }

    public List<Client> Clients => clients;

    [Dependency]
    protected EventSystem EventSystem = default!;

    private GameConfig gameConfig = new GameConfig() { TickFrequency = 20 };
    private DateTime lastNetworkUpdateTime = DateTime.Now;

    private List<Client> clients = new List<Client>();

    private Dictionary<NetworkConnection, PacketSerializer> packetSerializers = new Dictionary<NetworkConnection, PacketSerializer>();

    protected ILogger logger;

    public override void OnInitialize()
    {
        logger = LoggerService.GetSawmill("snake-server");

        Transport = new TcpTransport();
        Transport.Logger = logger;

        Transport.OnClientConnected = OnClientConnected;
        Transport.OnClientDisconnected = OnClientDisconnected;
        Transport.OnMessageReceived = OnMessageReceived;

        GameMode = new BaseGameMode();

        IoCManager.InjectDependencies(GameMode);
        EventSystem.SubscribeAll(GameMode);

        GameMode.Server = this;

        Simulation = new Simulation();

        Simulation.Simulate(() =>
        {
            GameMode.Start(Simulation);
        });
    }

    public override void OnUpdate(float deltaTime)
    {
        if (DateTime.Now - lastNetworkUpdateTime < TimeSpan.FromSeconds(1.0 / gameConfig.TickFrequency))
        {
            return;
        }
        lastNetworkUpdateTime = DateTime.Now;

        Simulation.Simulate(() =>
        {
            Transport.Update();

            GameMode.Update();

            // Go through every client and catch them up to the current tick we just simulated
            foreach (Client client in clients)
            {
                if (!client.IsSynced) { continue; } // dont send delta updates to clients that are not synced

                List<TickEvent> eventsToCatchUp = Simulation.Events.FindAll(e => e.Tick > client.LastTick);

                foreach (TickEvent tickEvent in eventsToCatchUp)
                {
                    SendEvent(client.Connection, tickEvent);
                }

                client.LastTick = Simulation.CurrentTick;

                // Update the client's packet serializer
                if (packetSerializers.ContainsKey(client.Connection))
                {
                    packetSerializers[client.Connection].CurrentGameTick = Simulation.CurrentTick;
                }
            }

            foreach ((NetworkConnection connection, PacketSerializer serializer) in packetSerializers)
            {
                if (connection.IsInvalid) { continue; }

                IWriteMessage packet = new WriteOnlyMessage();
                serializer.CurrentGameTick = (byte)(Simulation.CurrentTick % 255);
                if (serializer.BuildMessage(packet, logger))
                {
                    logger.LogVerbose($"Sending packet to {connection.Id}: {string.Join(" ", packet.Buffer.Take(packet.LengthBytes).Select(b => b.ToString("X2")))}");

                    Transport.SendToClient(packet, connection);
                }
            }
        });
    }

    public void SendEvent(NetworkConnection connection, TickEvent tickEvent)
    {
        switch (tickEvent)
        {
            case BoardResetEvent boardResetEvent:
                BoardResetNetMessage boardReset = new BoardResetNetMessage() { Width = boardResetEvent.Width, Height = boardResetEvent.Height };
                SendToClient(boardReset, ServerToClient.BoardReset, connection);
                break;
            case BoardSetEvent boardSetEvent:
                SendBoardMessage(boardSetEvent.X, boardSetEvent.Y, boardSetEvent.Tile);
                break;
            case SnakeMoveEvent snakeMoveEvent:
                PlayerMovedNetMessage snakeMove = new PlayerMovedNetMessage() { PlayerId = snakeMoveEvent.PlayerId, X = snakeMoveEvent.NewHeadPosition.X, Y = snakeMoveEvent.NewHeadPosition.Y, Grew = snakeMoveEvent.Grew };
                SendToClient(snakeMove, ServerToClient.PlayerMoved, connection);
                break;
            case SnakeSpawnEvent snakeSpawnEvent:
                PlayerSpawnedNetMessage playerSpawned = new PlayerSpawnedNetMessage() { PlayerId = connection.Id };
                SendToClient(playerSpawned, ServerToClient.PlayerSpawned, connection);

                SendBoardMessage(snakeSpawnEvent.X, snakeSpawnEvent.Y, new Tile { Type = TileType.SnakeHead, PlayerId = snakeSpawnEvent.PlayerId });
                break;
            case SnakeKillEvent snakeKillEvent:
                PlayerDiedNetMessage playerDied = new PlayerDiedNetMessage() { PlayerId = snakeKillEvent.PlayerId, RespawnTimeSeconds = 5 };
                SendToClient(playerDied, ServerToClient.PlayerDied, connection);
                break;
        }
    }

    public void Listen(int port)
    {
        packetSerializers.Clear();
        Transport.Listen(port);

        logger.LogInfo($"Listening on port {port}");
    }

    public void Shutdown()
    {
        Transport.Shutdown();
    }

    public void SendToClient(IWriteMessage message, ServerToClient type, NetworkConnection connection)
    {
        if (connection.IsInvalid)
        {
            logger.LogWarning($"Tried to send message to invalid connection {connection}");
            return;
        }

        if (!packetSerializers.ContainsKey(connection))
        {
            logger.LogWarning($"Tried to send message to connection without a packet serializer {connection}");
            return;
        }

        packetSerializers[connection].QueueMessage(message, type);
    }

    public void SendToClient(NetMessage message, ServerToClient type, NetworkConnection connection)
    {
        IWriteMessage writeMessage = new WriteOnlyMessage();
        message.Serialize(writeMessage);
        SendToClient(writeMessage, type, connection);
    }

    private void OnClientConnected(NetworkConnection connection)
    {
        packetSerializers[connection] = new PacketSerializer();

        logger.LogInfo($"Client connected: {connection.Id}");
    }

    private void OnClientDisconnected(NetworkConnection connection, DisconnectReason reason)
    {
        logger.LogInfo($"Client disconnected: {connection.Id}");

        packetSerializers.Remove(connection);
        clients.RemoveAll(c => c.Connection == connection);
    }

    private void OnMessageReceived(IReadMessage incomingMessage)
    {
        logger.LogVerbose($"Message received from {incomingMessage.Sender.Id}");
        logger.LogVerbose(string.Join(" ", incomingMessage.Buffer.Take(incomingMessage.LengthBytes).Select(b => b.ToString("X2"))));

        if (!packetSerializers.ContainsKey(incomingMessage.Sender))
        {
            logger.LogWarning($"Received message from connection without a packet serializer {incomingMessage.Sender}");
            return;
        }

        PacketSerializer packetDeserializer = packetSerializers[incomingMessage.Sender];

        packetDeserializer.ReadIncoming(incomingMessage, (ClientToServer messageType, IReadMessage message) =>
        {
            switch (messageType)
            {
                case ClientToServer.Connecting:
                    HandleConnecting(message);
                    break;
                case ClientToServer.Disconnecting:
                    HandleDisconnecting(message);
                    break;
                case ClientToServer.FullUpdate:
                    HandleFullUpdate(message);
                    break;
                case ClientToServer.PlayerInput:
                    HandlePlayerInput(message);
                    break;
                case ClientToServer.RequestLobbyInfo:
                    HandleRequestLobbyInfo(message);
                    break;
                case ClientToServer.ChangeName:
                    HandleChangeName(message);
                    break;
                case ClientToServer.SendChatMessage:
                    HandleSendChatMessage(message);
                    break;
            }
        }, logger);
    }

    private void SendBoardMessage(byte x, byte y, Tile tile)
    {
        IWriteMessage boardSetMessage = new WriteOnlyMessage();

        CellDataWithPos cellData = new CellDataWithPos
        {
            X = x,
            Y = y,
            Data = new CellData
            {
                Resource = tile.Type,
                AssociatedPlayerId = tile.PlayerId
            }
        };

        cellData.Serialize(boardSetMessage);

        foreach (Client client in clients)
        {
            SendToClient(boardSetMessage, ServerToClient.BoardSet, client.Connection);
        }
    }

    private void HandleRequestLobbyInfo(IReadMessage message)
    {
        LobbyInformation lobbyInfo = new LobbyInformation
        {
            PlayerCount = (byte)clients.Count,
            Title = "AAA",
            //Description = "I fucking hate drainage system. I mean first of all, WATER? Why the fuck is the protagonist, a slugcat, forced to go into water?? This is animal abuse and i will not stand for it. Secondly, THAT ONE FUCKING ROOM. you know the one. i was on my 237th cycle",
            Description="BBB",
            HostInfo = new HostInfo()
            {
                VersionMajor = 0,
                VersionMinor = 11,
                AgentString = "Evil Snake Server"
            }
        };
        IWriteMessage response = new WriteOnlyMessage();
        lobbyInfo.Serialize(response);
        SendToClient(response, ServerToClient.LobbyInformation, message.Sender);


        logger.LogInfo("Sent lobby information");
    }

    private void HandleConnecting(IReadMessage message)
    {
        if (clients.Select(c => c.Connection).Contains(message.Sender))
        {
            return;
        }

        ConnectingNetMessage connecting = new ConnectingNetMessage();
        connecting.Deserialize(message);

        clients.Add(new Client
        {
            Connection = message.Sender,
            Name = message.Sender.Id.ToString(),
        });

        logger.LogInfo($"Received connecting package {connecting} from {message.Sender}");

        IWriteMessage gameConfigMessage = new WriteOnlyMessage();
        gameConfig.Serialize(gameConfigMessage);
        SendToClient(gameConfigMessage, ServerToClient.GameConfig, message.Sender);

        IWriteMessage assignPlayerIdMessage = new WriteOnlyMessage();
        AssignPlayerId assignPlayerId = new AssignPlayerId() { PlayerId = message.Sender.Id };
        assignPlayerId.Serialize(assignPlayerIdMessage);
        SendToClient(assignPlayerIdMessage, ServerToClient.AssignPlayerId, message.Sender);

        PlayerConnected playerConnected = new PlayerConnected() { PlayerId = message.Sender.Id };
        IWriteMessage playerConnectedMessage = new WriteOnlyMessage();
        playerConnected.Serialize(playerConnectedMessage);

        PlayerRenamed playerRenamed = new PlayerRenamed() { PlayerId = message.Sender.Id, NewName = $"Player {message.Sender.Id}" };
        IWriteMessage playerRenamedMessage = new WriteOnlyMessage();
        playerRenamed.Serialize(playerRenamedMessage);

        foreach (Client client in clients)
        {
            SendToClient(playerConnectedMessage, ServerToClient.PlayerConnected, client.Connection);
            SendToClient(playerRenamedMessage, ServerToClient.PlayerRenamed, client.Connection);
        }
    }

    private void HandleFullUpdate(IReadMessage message)
    {
        // Send the newest board state to the client
        BoardResetNetMessage boardReset = new BoardResetNetMessage() { Width = Simulation.State.Board.Width, Height = Simulation.State.Board.Height };
        SendToClient(boardReset, ServerToClient.BoardReset, message.Sender);

        for (byte x = 0; x < Simulation.State.Board.Width; x++)
        {
            for (byte y = 0; y < Simulation.State.Board.Height; y++)
            {
                IWriteMessage boardSetMessage = new WriteOnlyMessage();

                Tile tile = Simulation.State.Board.GetResource(x, y);

                if (tile.Type == TileType.Empty) { continue; }

                CellDataWithPos cellData = new CellDataWithPos()
                {
                    X = x,
                    Y = y,
                    Data = new CellData
                    {
                        Resource = tile.Type,
                        AssociatedPlayerId = tile.PlayerId
                    }
                };

                cellData.Serialize(boardSetMessage);
                SendToClient(boardSetMessage, ServerToClient.BoardSet, message.Sender);
            }
        }

        // PlayerConnected
        foreach (Client client in clients)
        {
            if (client.Connection == message.Sender) { continue; }

            PlayerConnected playerConnected = new PlayerConnected() { PlayerId = client.Id };
            IWriteMessage playerConnectedMessage = new WriteOnlyMessage();
            playerConnected.Serialize(playerConnectedMessage);
            SendToClient(playerConnectedMessage, ServerToClient.PlayerConnected, message.Sender);

            IWriteMessage playerRenamedMessage = new WriteOnlyMessage();
            PlayerRenamed playerRenamed = new PlayerRenamed() { PlayerId = client.Id, NewName = $"Player {client.Id}" };
            playerRenamed.Serialize(playerRenamedMessage);
            SendToClient(playerRenamedMessage, ServerToClient.PlayerRenamed, message.Sender);
        }

        // PlayerSpawned
        foreach (Snake snake in Simulation.State.Snakes)
        {
            PlayerSpawnedNetMessage playerSpawned = new PlayerSpawnedNetMessage() { PlayerId = snake.PlayerId };
            IWriteMessage playerSpawnedMessage = new WriteOnlyMessage();
            playerSpawned.Serialize(playerSpawnedMessage);
            SendToClient(playerSpawnedMessage, ServerToClient.PlayerSpawned, message.Sender);
        }

        Client syncingClient = clients.Where(c => c.Connection == message.Sender).First();
        syncingClient.IsSynced = true;
        syncingClient.LastTick = Simulation.CurrentTick;

        logger.LogInfo($"Sent full update to {message.Sender}");
    }

    private void HandlePlayerInput(IReadMessage message)
    {
        byte input = message.ReadByte();

        PlayerInput playerInput = (PlayerInput)input;

        Client client = clients.Where(c => c.Connection == message.Sender).FirstOrDefault();
        if (client != null)
        {
            EventSystem.PublishEvent<IReceiveClientInput>(x => x.ReceiveInput(client, playerInput));
        }
    }

    private void HandleDisconnecting(IReadMessage message)
    {
        Transport.DisconnectClient(message.Sender, DisconnectReason.Unknown);
    }

    private void HandleChangeName(IReadMessage message)
    {
        ChangeName changeName = new ChangeName();
        changeName.Deserialize(message);
        logger.LogInfo($"Received name change to {message.Sender}: {changeName}");

        foreach (Client client in clients)
        {
            if (client.Connection == message.Sender)
            {
                client.Name = changeName.NewName;
            }
        }
    }

    private void HandleSendChatMessage(IReadMessage message)
    {
        string textMessage = message.ReadCharArray();

        logger.LogInfo($"Received chat message from {message.Sender}: {textMessage}");

        SendChatMessage chatMessageResponse = new SendChatMessage() { PlayerId = message.Sender.Id, Message = textMessage };
        IWriteMessage chatMessageResponseMessage = new WriteOnlyMessage();
        chatMessageResponse.Serialize(chatMessageResponseMessage);

        foreach (Client client in clients)
        {
            SendToClient(chatMessageResponseMessage, ServerToClient.ChatMessageSent, client.Connection);
        }
    }
}
