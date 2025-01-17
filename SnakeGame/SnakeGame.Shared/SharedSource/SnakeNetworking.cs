using MalignEngine;
using System.Net;

namespace SnakeGame;

public enum PlayerInput : byte
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3
}

public enum ServerToClient : byte
{
    LobbyInformation = 0,
    GameConfig = 1,
    AssignPlayerId = 2,
    BoardReset = 3,
    BoardSet = 4,
    PlayerConnected = 5,
    PlayerDisconnected = 6,
    PlayerSpawned = 7,
    PlayerDied = 8,
    PlayerMoved = 9,
    RespawnAllowed = 10,
    ChatMessageSent = 11,
}

public enum ClientToServer : byte
{
    RequestLobbyInfo = 0,
    Connecting = 1,
    Disconnecting = 2,
    FullUpdate = 3,
    PlayerInput = 4,
    RequestRespawn = 5,
    SendChatMessage = 6
}


public class TileData : NetMessage
{
    public TileType Resource;
    public byte AssociatedPlayerId;

    public override void Deserialize(IReadMessage message)
    {
        Resource = (TileType)message.ReadByte();
        AssociatedPlayerId = message.ReadByte();
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte((byte)Resource);
        message.WriteByte(AssociatedPlayerId);
    }
}

public class HostInfo : NetMessage
{
    public byte VersionMajor;
    public byte VersionMinor;
    public string AgentString;

    public override void Deserialize(IReadMessage message)
    {
        VersionMajor = message.ReadByte();
        VersionMinor = message.ReadByte();
        AgentString = message.ReadCharArray();
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(VersionMajor);
        message.WriteByte(VersionMinor);
        message.WriteCharArray(AgentString);
    }
}

public class GameConfig : NetMessage
{
    public byte TickFrequency;

    public override void Deserialize(IReadMessage message)
    {
        TickFrequency = message.ReadByte();
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(TickFrequency);
    }
}