using MalignEngine;
using Silk.NET.Maths;

namespace SnakeGame;

public class SnakeServer : EntitySystem
{
    public Transport Transport { get; private set; }
    public Board Board { get; private set; }
    public List<Snake> Snakes { get; private set; }

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

        Board = new Board(64, 64);
        Snakes = new List<Snake>();
    }

    public override void OnUpdate(float deltaTime)
    {
        Transport.Update();

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
        logger.LogVerbose($"Message received from {message.Sender.Id}");
        // print all bytes
        logger.LogVerbose(Convert.ToHexString(message.Buffer));

        byte clientTick = message.ReadByte();
        byte groupCount = message.ReadByte();

        for (int i = 0; i < groupCount; i++)
        {
            ClientToServer messageType = (ClientToServer)message.ReadByte();

            byte messageCount = message.ReadByte();
            ushort skipBytes = message.ReadUInt16();

            for (int j = 0; j < messageCount + 1; j++)
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
            }
        }
    }

    public IWriteMessage CreateMessage(ServerToClient messageType)
    {
        WriteOnlyMessage message = new WriteOnlyMessage();

        message.WriteByte(0);
        message.WriteByte(1);
        message.WriteByte((byte)messageType);
        message.WriteByte(0);
        message.WriteUInt16(0);

        return message;
    }

    public void SendMessageToClient(NetworkConnection connection, IWriteMessage message)
    {
        SendToClient(message, connection);
    }

    public void SendBoardMessage(byte x, byte y, Tile tile)
    {
        IWriteMessage boardSetMessage = CreateMessage(ServerToClient.BoardSet);

        TileData cellData = new TileData
        {
            Resource = tile.Type,
            AssociatedPlayerId = tile.PlayerId
        };

        cellData.Serialize(boardSetMessage);

        foreach (NetworkConnection client in clients)
        {
            SendMessageToClient(client, boardSetMessage);
        }
    }

    private void HandleRequestLobbyInfo(IReadMessage message)
    {
        IWriteMessage response = CreateMessage(ServerToClient.LobbyInformation);

        response.WriteByte((byte)clients.Count);
        response.WriteCharArray("Funny lobby");
        response.WriteCharArray("I fucking hate drainage system. I mean first of all, WATER? Why the fuck is the protagonist, a slugcat, forced to go into water?? This is animal abuse and i will not stand for it. Secondly, THAT ONE FUCKING ROOM. you know the one. i was on my 237th cycle");

        HostInfo hostInfo = new HostInfo
        {
            VersionMajor = 0,
            VersionMinor = 0,
            AgentString = "Evil Snake Server"
        };

        hostInfo.Serialize(response);

        SendMessageToClient(message.Sender, response);

        logger.LogInfo("Sent lobby information");
    }

    private void HandleConnecting(IReadMessage message)
    {
        if (clients.Contains(message.Sender))
        {
            return;
        }

        clients.Add(message.Sender);

        string name = message.ReadCharArray();
        HostInfo hostInfo = new HostInfo();
        hostInfo.Deserialize(message);

        logger.LogInfo($"Client {message.Sender.Id} connected with name {name} and agent {hostInfo.AgentString}, snake {hostInfo.VersionMajor}.{hostInfo.VersionMinor}");

        IWriteMessage gameConfigMessage = CreateMessage(ServerToClient.GameConfig);
        GameConfig gameConfig = new GameConfig() { TickFrequency = 60 };
        gameConfig.Serialize(gameConfigMessage);
        SendMessageToClient(message.Sender, gameConfigMessage);

        IWriteMessage assignPlayerIdMessage = CreateMessage(ServerToClient.AssignPlayerId);
        assignPlayerIdMessage.WriteByte(message.Sender.Id);
        SendMessageToClient(message.Sender, assignPlayerIdMessage);
    }

    private void HandleFullUpdate(IReadMessage message)
    {
        IWriteMessage boardResetMessage = CreateMessage(ServerToClient.BoardReset);
        boardResetMessage.WriteByte(Board.Width);
        boardResetMessage.WriteByte(Board.Height);
        SendMessageToClient(message.Sender, boardResetMessage);

        // Board Set
        for (byte x = 0; x < Board.Width; x++)
        {
            for (byte y = 0; y < Board.Height; y++)
            {
                IWriteMessage boardSetMessage = CreateMessage(ServerToClient.BoardSet);

                Tile tile = Board.GetResource(x, y);

                if (tile.Type == TileType.Empty) { continue; }

                TileData cellData = new TileData
                {
                    Resource = tile.Type,
                    AssociatedPlayerId = tile.PlayerId
                };

                cellData.Serialize(boardSetMessage);

                SendMessageToClient(message.Sender, boardSetMessage);
            }
        }

        // PlayerConnected
        IWriteMessage playerConnectedMessage = CreateMessage(ServerToClient.PlayerConnected);
        playerConnectedMessage.WriteByte(message.Sender.Id);
        playerConnectedMessage.WriteCharArray($"Player {message.Sender.Id}");

        foreach (NetworkConnection client in clients)
        {
            SendMessageToClient(client, playerConnectedMessage);
        }

        // PlayerSpawned
        foreach (Snake snake in Snakes)
        {
            IWriteMessage playerSpawnedMessage = CreateMessage(ServerToClient.PlayerSpawned);
            playerSpawnedMessage.WriteByte(snake.PlayerId);
            SendMessageToClient(message.Sender, playerSpawnedMessage);
        }

        // RespawnAllowed
        IWriteMessage respawnAllowedMessage = CreateMessage(ServerToClient.RespawnAllowed);
        SendMessageToClient(message.Sender, respawnAllowedMessage);

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

        IWriteMessage playerSpawnedMessage = CreateMessage(ServerToClient.PlayerSpawned);
        playerSpawnedMessage.WriteByte(message.Sender.Id);
        SendMessageToClient(message.Sender, playerSpawnedMessage);

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
}
