using MalignEngine;
using Silk.NET.Maths;
using TcpTransport = SnakeGame.TcpTransport;

namespace SnakeGame;

public class SnakeServer : EntitySystem
{
    private class QueuedSendMessage
    {
        public IWriteMessage Message { get; set; }
    }

    [Dependency]
    protected IUpdateLoop UpdateLoop = default!;

    public Transport Transport { get; private set; }
    public Board Board { get; private set; }
    public List<Snake> Snakes { get; private set; }

    private GameConfig gameConfig = new GameConfig() { TickFrequency = 20 };
    private DateTime lastNetworkUpdateTime = DateTime.Now;

    private float movesPerSecond = 10f;
    private DateTime lastMoveTime = DateTime.Now;

    private List<NetworkConnection> clients = new List<NetworkConnection>();

    private Dictionary<NetworkConnection, PacketSerializer> packetSerializers = new Dictionary<NetworkConnection, PacketSerializer>();

    private Queue<(NetworkConnection, DateTime)> respawnQueue = new Queue<(NetworkConnection, DateTime)>();

    private byte gameTick = 0;

    private float respawnTime = 1f;

    private float foodSpawnTime = 2f;
    private DateTime lastFoodSpawn = DateTime.Now;

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

        for (int x = 5; x < 50;  x++)
        {
            Board.SetResource((byte)x, 5, new Tile { Type = TileType.Wall, PlayerId = 0 });
            Board.SetResource((byte)x, 8, new Tile { Type = TileType.Wall, PlayerId = 0 });
        }
    }

    public override void OnUpdate(float deltaTime)
    {
        if (DateTime.Now - lastMoveTime > TimeSpan.FromSeconds(1.0 / movesPerSecond))
        {
            lastMoveTime = DateTime.Now;

            List<Snake> snakes = new List<Snake>(Snakes);

            foreach (Snake snake in snakes)
            {
                if (snake.Killed) { continue; }

                MoveSnake(snake);
            }
        }

        if (Snakes.Count > 0 && DateTime.Now - lastFoodSpawn > TimeSpan.FromSeconds(foodSpawnTime))
        {
            lastFoodSpawn = DateTime.Now;

            byte x = (byte)Random.Shared.Next(0, Board.Width);
            byte y = (byte)Random.Shared.Next(0, Board.Height);

            if (Board.GetResource(x, y).Type == TileType.Empty)
            {
                Board.SetResource(x, y, new Tile { Type = TileType.Food, PlayerId = 0 });
                SendBoardMessage(x, y, new Tile { Type = TileType.Food, PlayerId = 0 });
            }
        }

        while (respawnQueue.Count > 0)
        {
            (NetworkConnection connection, DateTime time) = respawnQueue.Peek();

            if (DateTime.Now > time)
            {
                respawnQueue.Dequeue();
                if (!connection.IsInvalid)
                {
                    SpawnClient(connection);
                }
            }
            else
            {
                break;
            }
        }

        if (DateTime.Now - lastNetworkUpdateTime > TimeSpan.FromSeconds(1.0 / gameConfig.TickFrequency))
        {
            Transport.Update();

            foreach ((NetworkConnection connection, PacketSerializer serializer) in packetSerializers)
            {
                if (connection.IsInvalid) { continue; }

                IWriteMessage packet = new WriteOnlyMessage();
                serializer.CurrentGameTick = gameTick;
                if (serializer.BuildMessage(packet, logger))
                {
                    logger.LogVerbose($"Sending packet to {connection.Id}: {string.Join(" ", packet.Buffer.Take(packet.LengthBytes).Select(b => b.ToString("X2")))}");

                    Transport.SendToClient(packet, connection);
                    gameTick++;
                }
            }

            lastNetworkUpdateTime = DateTime.Now;
        }
    }

    public void SpawnSnake(byte playerId, byte positionX, byte positionY)
    {
        Snake snake = new Snake();
        snake.PlayerId = playerId;
        snake.Input = new PlayerInput();
        snake.HeadPosition = new Vector2D<byte>(positionX, positionY);
        snake.BodyPositions.Add(new Vector2D<byte>((byte)(positionX), (byte)(positionY + 1 + playerId)));
        snake.BodyPositions.Add(new Vector2D<byte>((byte)(positionX), (byte)(positionY + 2 + playerId)));
        snake.BodyPositions.Add(new Vector2D<byte>((byte)(positionX), (byte)(positionY + 3 + playerId)));

        Board.SetResource(snake.HeadPosition.X, snake.HeadPosition.Y, new Tile { Type = TileType.SnakeHead, PlayerId = playerId });
        Board.SetResource(snake.BodyPositions[0].X, snake.BodyPositions[0].Y, new Tile { Type = TileType.SnakeBody, PlayerId = playerId });
        Board.SetResource(snake.BodyPositions[1].X, snake.BodyPositions[1].Y, new Tile { Type = TileType.SnakeBody, PlayerId = playerId });
        Board.SetResource(snake.BodyPositions[2].X, snake.BodyPositions[2].Y, new Tile { Type = TileType.SnakeBody, PlayerId = playerId });

        Snakes.Add(snake);
    }

    public void KillSnake(Snake snake)
    {
        Snakes.Remove(snake);
        snake.Killed = true;

        foreach (Vector2D<byte> bodyPosition in snake.BodyPositions)
        {
            if (Random.Shared.Next(0, 100) > 50)
            {
                Board.SetResource(bodyPosition.X, bodyPosition.Y, new Tile { Type = TileType.Food, PlayerId = snake.PlayerId });
                SendBoardMessage(bodyPosition.X, bodyPosition.Y, new Tile { Type = TileType.Food, PlayerId = snake.PlayerId });
            }
            else
            {
                Board.SetResource(bodyPosition.X, bodyPosition.Y, new Tile { Type = TileType.Empty, PlayerId = 0 });
                SendBoardMessage(bodyPosition.X, bodyPosition.Y, new Tile { Type = TileType.Empty, PlayerId = 0 });
            }
        }

        Board.SetResource(snake.HeadPosition.X, snake.HeadPosition.Y, new Tile { Type = TileType.Empty, PlayerId = 0 });
        SendBoardMessage(snake.HeadPosition.X, snake.HeadPosition.Y, new Tile { Type = TileType.Empty, PlayerId = 0 });

        PlayerDied playerDied = new PlayerDied() { PlayerId = snake.PlayerId, RespawnTimeSeconds = (byte)respawnTime };
        IWriteMessage playerDiedMessage = new WriteOnlyMessage();
        playerDied.Serialize(playerDiedMessage);

        foreach (NetworkConnection client in clients)
        {
            SendToClient(playerDiedMessage, ServerToClient.PlayerDied, client);
        }

        NetworkConnection connection = clients.Find(client => client.Id == snake.PlayerId);

        if (connection != null)
        {
            respawnQueue.Enqueue((connection, DateTime.Now + TimeSpan.FromSeconds(respawnTime)));
        }
    }

    private void MoveSnake(Snake snake)
    {
        Vector2D<byte> newHeadPosition = snake.HeadPosition;

        switch (snake.Input)
        {
            case PlayerInput.Up:
                newHeadPosition.Y--;
                break;
            case PlayerInput.Down:
                newHeadPosition.Y++;
                break;
            case PlayerInput.Left:
                newHeadPosition.X--;
                break;
            case PlayerInput.Right:
                newHeadPosition.X++;
                break;
        }

        // Check if the new head position is out of bounds and teleport the snake to the other side of the board
        if (newHeadPosition.X <= 0)
        {
            newHeadPosition.X = (byte)(Board.Width - 1);
        }
        else if (newHeadPosition.X >= Board.Width)
        {
            newHeadPosition.X = 0;
        }

        if (newHeadPosition.Y <= 0)
        {
            newHeadPosition.Y = (byte)(Board.Height - 1);
        }
        else if (newHeadPosition.Y >= Board.Height)
        {
            newHeadPosition.Y = 0;
        }

        // Check if the head position will end up in another snake
        foreach (Snake otherSnake in Snakes)
        {
            //if (otherSnake.PlayerId == snake.PlayerId) { continue; }

            if (otherSnake.HeadPosition == newHeadPosition)
            {
                // Kill both snakes
                KillSnake(snake);
                KillSnake(otherSnake);
                return;
            }

            if (otherSnake.BodyPositions.Contains(newHeadPosition))
            {
                // Kill the snake
                KillSnake(snake);
                return;
            }
        }

        bool grow = false;

        // Check if the head position will end up in food
        Tile tile = Board.GetResource(newHeadPosition.X, newHeadPosition.Y);
        if (tile.Type == TileType.Food)
        {
            grow = true;
        }
        else if (tile.Type == TileType.Wall)
        {
            KillSnake(snake);
            return;
        }

        snake.BodyPositions.Insert(0, snake.HeadPosition);
        snake.HeadPosition = newHeadPosition;

        Board.SetResource(snake.HeadPosition.X, snake.HeadPosition.Y, new Tile { Type = TileType.SnakeHead, PlayerId = snake.PlayerId });
        Board.SetResource(snake.BodyPositions[0].X, snake.BodyPositions[0].Y, new Tile { Type = TileType.SnakeBody, PlayerId = snake.PlayerId });

        SendBoardMessage(snake.HeadPosition.X, snake.HeadPosition.Y, new Tile { Type = TileType.SnakeHead, PlayerId = snake.PlayerId });
        SendBoardMessage(snake.BodyPositions[0].X, snake.BodyPositions[0].Y, new Tile { Type = TileType.SnakeBody, PlayerId = snake.PlayerId });

        if (!grow)
        {
            int indexToRemove = snake.BodyPositions.Count - 1;
            Board.SetResource(snake.BodyPositions[indexToRemove].X, snake.BodyPositions[indexToRemove].Y, new Tile { Type = TileType.Empty, PlayerId = 0 });
            SendBoardMessage(snake.BodyPositions[indexToRemove].X, snake.BodyPositions[indexToRemove].Y, new Tile { Type = TileType.Empty, PlayerId = 0 });
            snake.BodyPositions.RemoveAt(indexToRemove);
        }

        IWriteMessage playerMovedMessage = new WriteOnlyMessage();
        PlayerMoved playerMoved = new PlayerMoved() { Grew = grow, PlayerId = snake.PlayerId, X = snake.HeadPosition.X, Y = snake.HeadPosition.Y };
        playerMoved.Serialize(playerMovedMessage);
        foreach (NetworkConnection client in clients)
        {
            SendToClient(playerMovedMessage, ServerToClient.PlayerMoved, client);
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

    public void OnClientConnected(NetworkConnection connection)
    {
        packetSerializers[connection] = new PacketSerializer();

        logger.LogInfo($"Client connected: {connection.Id}");
    }

    public void OnClientDisconnected(NetworkConnection connection, DisconnectReason reason)
    {
        logger.LogInfo($"Client disconnected: {connection.Id}");

        packetSerializers.Remove(connection);
        clients.Remove(connection);

        // Kill all snakes belonging to the disconnected client
        foreach (Snake snake in Snakes.FindAll(snake => snake.PlayerId == connection.Id))
        {
            KillSnake(snake);
        }
    }

    public void OnMessageReceived(IReadMessage incomingMessage)
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

    public void SendBoardMessage(byte x, byte y, Tile tile)
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
                VersionMinor = 10,
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
        gameConfig.Serialize(gameConfigMessage);
        SendToClient(gameConfigMessage, ServerToClient.GameConfig, message.Sender);

        IWriteMessage assignPlayerIdMessage = new WriteOnlyMessage();
        AssignPlayerId assignPlayerId = new AssignPlayerId() { PlayerId = message.Sender.Id };
        assignPlayerId.Serialize(assignPlayerIdMessage);
        SendToClient(assignPlayerIdMessage, ServerToClient.AssignPlayerId, message.Sender);

        PlayerConnected playerConnected = new PlayerConnected() { PlayerId = message.Sender.Id, Name = $"Player {message.Sender.Id}" };
        IWriteMessage playerConnectedMessage = new WriteOnlyMessage();
        playerConnected.Serialize(playerConnectedMessage);

        foreach (NetworkConnection client in clients)
        {
            SendToClient(playerConnectedMessage, ServerToClient.PlayerConnected, client);
        }
    }

    private void HandleFullUpdate(IReadMessage message)
    {
        BoardReset boardReset = new BoardReset() { Width = Board.Width, Height = Board.Height };
        IWriteMessage boardResetMessage = new WriteOnlyMessage();
        boardReset.Serialize(boardResetMessage);
        SendToClient(boardResetMessage, ServerToClient.BoardReset, message.Sender);

        // Board Set
        for (byte x = 0; x < Board.Width; x++)
        {
            for (byte y = 0; y < Board.Height; y++)
            {
                IWriteMessage boardSetMessage = new WriteOnlyMessage();

                Tile tile = Board.GetResource(x, y);

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
        foreach (NetworkConnection client in clients)
        {
            if (client == message.Sender) { continue; }

            PlayerConnected playerConnected = new PlayerConnected() { PlayerId = client.Id, Name = $"Player {client.Id}" };
            IWriteMessage playerConnectedMessage = new WriteOnlyMessage();
            playerConnected.Serialize(playerConnectedMessage);
            SendToClient(playerConnectedMessage, ServerToClient.PlayerConnected, message.Sender);
        }

        // PlayerSpawned
        foreach (Snake snake in Snakes)
        {
            PlayerSpawned playerSpawned = new PlayerSpawned() { PlayerId = snake.PlayerId };
            IWriteMessage playerSpawnedMessage = new WriteOnlyMessage();
            playerSpawned.Serialize(playerSpawnedMessage);
            SendToClient(playerSpawnedMessage, ServerToClient.PlayerSpawned, message.Sender);
        }

        // RespawnAllowed
        SendToClient(new WriteOnlyMessage(), ServerToClient.RespawnAllowed, message.Sender);

        logger.LogInfo($"Sent full update to {message.Sender}");
    }

    private void SpawnClient(NetworkConnection connection)
    {
        // Check if snake is already in the game
        if (Snakes.Any(snake => snake.PlayerId == connection.Id))
        {
            return;
        }

        SpawnSnake(connection.Id, 25, 25);

        PlayerSpawned playerSpawned = new PlayerSpawned() { PlayerId = connection.Id };
        IWriteMessage playerSpawnedMessage = new WriteOnlyMessage();
        playerSpawned.Serialize(playerSpawnedMessage);
        SendToClient(playerSpawnedMessage, ServerToClient.PlayerSpawned, connection);

        logger.LogInfo($"Spawned snake for client {connection}");
    }

    private void HandlePlayerInput(IReadMessage message)
    {
        byte input = message.ReadByte();

        if (input > 5) { return; }

        PlayerInput playerInput = (PlayerInput)input;

        if (playerInput == PlayerInput.Respawn)
        {
            SpawnClient(message.Sender);
        }
        else
        {
            Snake? snake = Snakes.Find(snake => snake.PlayerId == message.Sender.Id);

            if (snake == null) { return; }

            snake.Input = (PlayerInput)input;
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
        logger.LogInfo($"Received name change from {message.Sender}: {changeName}");
    }

    private void HandleSendChatMessage(IReadMessage message)
    {
        SendChatMessage chatMessage = new SendChatMessage();
        chatMessage.Deserialize(message);

        logger.LogInfo($"Received chat message from {message.Sender}: {chatMessage.Message}");

        SendChatMessage chatMessageResponse = new SendChatMessage() { PlayerId = chatMessage.PlayerId, Message = chatMessage.Message };
        IWriteMessage chatMessageResponseMessage = new WriteOnlyMessage();
        chatMessageResponse.Serialize(chatMessageResponseMessage);

        foreach (NetworkConnection client in clients)
        {
            SendToClient(chatMessageResponseMessage, ServerToClient.ChatMessageSent, client);
        }
    }
}
