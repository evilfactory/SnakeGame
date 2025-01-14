using MalignEngine;
using System.Net;

namespace SnakeGame;

public enum ServerToClient : byte
{
    LobbyInformation = 0,
    GameConfig = 1,
    AssignPlayerId = 2,
    BoardReset = 3,
    BoardSet = 4,
    BoardReplace = 5,
    PlayerConnected = 6,
    PlayerDisconnected = 7,
    PlayerSpawned = 8,
    PlayerDied = 9,
    PlayerMoved = 10,
    RespawnAllowed = 11,
    ChatMessageSent = 12,
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