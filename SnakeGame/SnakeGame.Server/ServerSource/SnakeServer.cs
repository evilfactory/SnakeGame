using MalignEngine;
using Silk.NET.Maths;

namespace SnakeGame;

public class SnakeServer : EntitySystem
{
    private class QueuedSendMessage
    {
        public IWriteMessage Message { get; set; }
    }

    public Transport Transport { get; private set; }
    public Board Board { get; private set; }
    public List<Snake> Snakes { get; private set; }

    private List<NetworkConnection> clients = new List<NetworkConnection>();

    private Dictionary<NetworkConnection, PacketSerializer> packetSerializers = new Dictionary<NetworkConnection, PacketSerializer>();
    private PacketDeserializer packetDeserializer = new PacketDeserializer();

    protected ILogger logger;

    public override void OnInitialize()
    {
        logger = LoggerService.GetSawmill("snake-server");

        Transport = new TcpTransport();
        Transport.Logger = logger;

        Transport.OnClientConnected = OnClientConnected;
        Transport.OnClientDisconnected = OnClientDisconnected;
        Transport.OnMessageReceived = OnMessageReceived;

        Board = new Board(64, 64);
        Snakes = new List<Snake>();
    }

    public override void OnUpdate(float deltaTime)
    {
        Transport.Update();

        foreach ((NetworkConnection connection, PacketSerializer serializer) in packetSerializers)
        {
            IWriteMessage packet = new WriteOnlyMessage();
            if (serializer.BuildMessage(packet))
            {
                Transport.SendToClient(packet, connection);
            }
        }

        foreach (Snake snake in Snakes)
        {
            MoveSnake(snake);
        }
    }

    public void SpawnSnake(byte playerId, byte positionX, byte positionY)
    {
        Snake snake = new Snake();
        snake.PlayerId = playerId;
        snake.Input = new PlayerInput();
        snake.HeadPosition = new Vector2D<byte>(positionX, positionY);
        snake.BodyPositions.Add(new Vector2D<byte>((byte)(positionX - 1), positionY));

        Board.SetResource(snake.HeadPosition.X, snake.HeadPosition.Y, new Tile { Type = TileType.SnakeHead, PlayerId = playerId });
        Board.SetResource(snake.BodyPositions[0].X, snake.BodyPositions[0].Y, new Tile { Type = TileType.SnakeBody, PlayerId = playerId });

        Snakes.Add(snake);
    }

    private void MoveSnake(Snake snake)
    {
        Vector2D<byte> newHeadPosition = snake.HeadPosition;

        switch (snake.Input)
        {
            case PlayerInput.Up:
                newHeadPosition.Y++;
                break;
            case PlayerInput.Down:
                newHeadPosition.Y--;
                break;
            case PlayerInput.Left:
                newHeadPosition.X--;
                break;
            case PlayerInput.Right:
                newHeadPosition.X++;
                break;
        }

        // Check if the new head position is out of bounds and teleport the snake to the other side of the board
        if (newHeadPosition.X < 0)
        {
            newHeadPosition.X = (byte)(Board.Width - 1);
        }
        else if (newHeadPosition.X >= Board.Width)
        {
            newHeadPosition.X = 0;
        }

        if (newHeadPosition.Y < 0)
        {
            newHeadPosition.Y = (byte)(Board.Height - 1);
        }
        else if (newHeadPosition.Y >= Board.Height)
        {
            newHeadPosition.Y = 0;
        }

        snake.BodyPositions.Insert(0, snake.HeadPosition);
        snake.HeadPosition = newHeadPosition;

        Board.SetResource(snake.HeadPosition.X, snake.HeadPosition.Y, new Tile { Type = TileType.SnakeHead, PlayerId = snake.PlayerId });
        Board.SetResource(snake.BodyPositions[0].X, snake.BodyPositions[0].Y, new Tile { Type = TileType.SnakeBody, PlayerId = snake.PlayerId });

        SendBoardMessage(snake.HeadPosition.X, snake.HeadPosition.Y, new Tile { Type = TileType.SnakeHead, PlayerId = snake.PlayerId });
        SendBoardMessage(snake.BodyPositions[0].X, snake.BodyPositions[0].Y, new Tile { Type = TileType.SnakeBody, PlayerId = snake.PlayerId });


        int indexToRemove = snake.BodyPositions.Count - 1;
        Board.SetResource(snake.BodyPositions[indexToRemove].X, snake.BodyPositions[indexToRemove].Y, new Tile { Type = TileType.Empty, PlayerId = 0 });
        SendBoardMessage(snake.BodyPositions[indexToRemove].X, snake.BodyPositions[indexToRemove].Y, new Tile { Type = TileType.Empty, PlayerId = 0 });
        snake.BodyPositions.RemoveAt(indexToRemove);
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
            packetSerializers[connection] = new PacketSerializer();
        }

        packetSerializers[connection].QueueMessage(message, type);
    }

    public void OnClientConnected(NetworkConnection connection)
    {
        logger.LogInfo($"Client connected: {connection.Id}");
    }

    public void OnClientDisconnected(NetworkConnection connection, DisconnectReason reason)
    {
        logger.LogInfo($"Client disconnected: {connection.Id}");

        packetSerializers.Remove(connection);
        clients.Remove(connection);
    }

    public void OnMessageReceived(IReadMessage incomingMessage)
    {
        logger.LogVerbose($"Message received from {incomingMessage.Sender.Id}");
        // print all hexadecimal bytes but also space them out
        logger.LogVerbose(string.Join(" ", incomingMessage.Buffer.Select(b => b.ToString("X2"))));

        var result = packetDeserializer.ReadIncoming(incomingMessage, (ClientToServer messageType, IReadMessage message) =>
        {
            switch (messageType)
            {
                case ClientToServer.RequestLobbyInfo:
                    HandleRequestLobbyInfo(message);
                    break;
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
                case ClientToServer.RequestRespawn:
                    HandleRequestRespawn(message);
                    break;
                case ClientToServer.SendChatMessage:
                    break;
            }
        });

        foreach (var error in result.Errors)
        {
            logger.LogError(error.Message);
        }
    }

    public void SendBoardMessage(byte x, byte y, Tile tile)
    {
        IWriteMessage boardSetMessage = new WriteOnlyMessage();

        TileData cellData = new TileData
        {
            Resource = tile.Type,
            AssociatedPlayerId = tile.PlayerId
        };

        cellData.Serialize(boardSetMessage);

        foreach (NetworkConnection client in clients)
        {
            SendToClient(boardSetMessage, ServerToClient.BoardSet, client);
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
                VersionMinor = 7,
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
        if (clients.Contains(message.Sender))
        {
            return;
        }

        Connecting connecting = new Connecting();
        connecting.Deserialize(message);

        clients.Add(message.Sender);

        logger.LogInfo($"Received connecting package {connecting} from {message.Sender}");

        IWriteMessage gameConfigMessage = new WriteOnlyMessage();
        GameConfig gameConfig = new GameConfig() { TickFrequency = 20 };
        gameConfig.Serialize(gameConfigMessage);
        SendToClient(gameConfigMessage, ServerToClient.GameConfig, message.Sender);

        IWriteMessage assignPlayerIdMessage = new WriteOnlyMessage();
        AssignPlayerId assignPlayerId = new AssignPlayerId() { PlayerId = message.Sender.Id };
        assignPlayerId.Serialize(assignPlayerIdMessage);
        SendToClient(assignPlayerIdMessage, ServerToClient.AssignPlayerId, message.Sender);

        IWriteMessage playerConnectedMessage = new WriteOnlyMessage();
        playerConnectedMessage.WriteByte(message.Sender.Id);
        playerConnectedMessage.WriteCharArray($"Player {message.Sender.Id}");
        
        foreach (NetworkConnection client in clients)
        {
            SendToClient(playerConnectedMessage, ServerToClient.PlayerConnected, client);
        }
    }

    private void HandleFullUpdate(IReadMessage message)
    {
        IWriteMessage boardResetMessage = new WriteOnlyMessage();
        boardResetMessage.WriteByte(Board.Width);
        boardResetMessage.WriteByte(Board.Height);
        SendToClient(boardResetMessage, ServerToClient.BoardReset, message.Sender);

        // Board Set
        for (byte x = 0; x < Board.Width; x++)
        {
            for (byte y = 0; y < Board.Height; y++)
            {
                IWriteMessage boardSetMessage = new WriteOnlyMessage();

                Tile tile = Board.GetResource(x, y);

                if (tile.Type == TileType.Empty) { continue; }

                TileData cellData = new TileData
                {
                    Resource = tile.Type,
                    AssociatedPlayerId = tile.PlayerId
                };

                cellData.Serialize(boardSetMessage);

                SendToClient(boardSetMessage, ServerToClient.BoardSet, message.Sender);
            }
        }

        // PlayerConnected
        foreach (NetworkConnection client in clients)
        {
            IWriteMessage playerConnectedMessage = new WriteOnlyMessage();
            playerConnectedMessage.WriteByte(client.Id);
            playerConnectedMessage.WriteCharArray($"Player {client.Id}");
            SendToClient(playerConnectedMessage, ServerToClient.PlayerConnected, message.Sender);
        }


        // PlayerSpawned
        foreach (Snake snake in Snakes)
        {
            IWriteMessage playerSpawnedMessage = new WriteOnlyMessage();
            playerSpawnedMessage.WriteByte(snake.PlayerId);
            SendToClient(playerSpawnedMessage, ServerToClient.PlayerSpawned, message.Sender);
        }

        // RespawnAllowed
        IWriteMessage respawnAllowedMessage = new WriteOnlyMessage();
        SendToClient(respawnAllowedMessage, ServerToClient.RespawnAllowed, message.Sender);

        logger.LogInfo($"Sent full update to {message.Sender}");
    }

    private void HandleRequestRespawn(IReadMessage message)
    {
        // Check if snake is already in the game
        if (Snakes.Any(snake => snake.PlayerId == message.Sender.Id))
        {
            return;
        }

        SpawnSnake(message.Sender.Id, 25, 25);

        IWriteMessage playerSpawnedMessage = new WriteOnlyMessage();
        playerSpawnedMessage.WriteByte(message.Sender.Id);
        SendToClient(playerSpawnedMessage, ServerToClient.PlayerSpawned, message.Sender);

        logger.LogInfo($"Spawned snake for client {message.Sender}");
    }

    private void HandlePlayerInput(IReadMessage message)
    {
        byte input = message.ReadByte();

        if (input > 4) { return; }

        Snake? snake = Snakes.Find(snake => snake.PlayerId == message.Sender.Id);

        if (snake == null) { return; }

        snake.Input = (PlayerInput)input;
    }

    private void HandleDisconnecting(IReadMessage message)
    {
        Transport.DisconnectClient(message.Sender, DisconnectReason.Unknown);
    }
}
