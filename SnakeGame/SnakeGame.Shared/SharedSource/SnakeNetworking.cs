using MalignEngine;
using FluentResults;

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

public class LobbyInformation : NetMessage
{
    public byte PlayerCount;
    public string Title;
    public string Description;
    public HostInfo HostInfo;

    public override void Deserialize(IReadMessage message)
    {
        PlayerCount = message.ReadByte();
        Title = message.ReadCharArray();
        Description = message.ReadCharArray();
        HostInfo = new HostInfo();
        HostInfo.Deserialize(message);
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(PlayerCount);
        message.WriteCharArray(Title);
        message.WriteCharArray(Description);
        HostInfo.Serialize(message);
    }

    public override string ToString()
    {
        return $"LobbyInformation ( PlayerCount: {PlayerCount}, Title: {Title}, Description: {Description}, HostInfo: {HostInfo} )";
    }
}

public class Connecting : NetMessage
{
    public string Name;
    public HostInfo HostInfo;
    public override void Deserialize(IReadMessage message)
    {
        Name = message.ReadCharArray();
        HostInfo = new HostInfo();
        HostInfo.Deserialize(message);
    }
    public override void Serialize(IWriteMessage message)
    {
        message.WriteCharArray(Name);
        HostInfo.Serialize(message);
    }
    public override string ToString()
    {
        return $"Connecting ( Name: {Name}, HostInfo: {HostInfo} )";
    }
}

public class AssignPlayerId : NetMessage
{
    public byte PlayerId;

    public override void Deserialize(IReadMessage message)
    {
        PlayerId = message.ReadByte();
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(PlayerId);
    }

    public override string ToString()
    {
        return $"AssignPlayerId ( PlayerId: {PlayerId} )";
    }
}

public class PlayerConnected : NetMessage
{
    public byte PlayerId;
    public string Name;
    public override void Deserialize(IReadMessage message)
    {
        PlayerId = message.ReadByte();
        Name = message.ReadCharArray();
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(PlayerId);
        message.WriteCharArray(Name);
    }

    public override string ToString()
    {
        return $"PlayerConnected ( PlayerId: {PlayerId}, Name: {Name} )";
    }
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

    public override string ToString()
    {
        return $"HostInfo ( VersionMajor: {VersionMajor}, VersionMinor: {VersionMinor}, AgentString: {AgentString} )";
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

    public override string ToString()
    {
        return $"GameConfig ( TickFrequency: {TickFrequency} )";
    }
}

public class PacketDeserializer
{
#if SERVER
    public Result ReadIncoming(IReadMessage message, Action<ClientToServer, IReadMessage> read)
#elif CLIENT
    public Result ReadIncoming(IReadMessage message, Action<ServerToClient, IReadMessage> read)
#endif
    {
        Result result = Result.Ok();

        byte clientTick = message.ReadByte();
        byte groupCount = message.ReadByte();

        for (int i = 0; i < groupCount; i++)
        {
#if SERVER
            ClientToServer messageType = (ClientToServer)message.ReadByte();
#elif CLIENT
            ServerToClient messageType = (ServerToClient)message.ReadByte();
#endif

            byte messageCount = message.ReadByte();
            ushort skipBytes = message.ReadUInt16();

            int sizeAfterRead = message.BytePosition + skipBytes;

            for (int j = 0; j < messageCount + 1; j++)
            {
                read(messageType, message);
            }

            if (sizeAfterRead != message.BytePosition)
            {
                result = result.WithError($"The message size did not match the skip bytes value, possibly malformed message? connection = {message.Sender}, messageType = {messageType}, messageCount = {messageCount}, skipBytes = {skipBytes}, sizeAfterRead = {sizeAfterRead}, messagePosition = {message.BytePosition}");
            }
        }

        return result;
    }
}

public class PacketSerializer
{
#if SERVER
    private Dictionary<ServerToClient, Queue<IWriteMessage>> queuedSendMessages = new Dictionary<ServerToClient, Queue<IWriteMessage>>();
#elif CLIENT
    private Dictionary<ClientToServer, Queue<IWriteMessage>> queuedSendMessages = new Dictionary<ClientToServer, Queue<IWriteMessage>>();
#endif

#if SERVER
    public void QueueMessage(IWriteMessage message, ServerToClient type)
    {
        if (!queuedSendMessages.ContainsKey(type))
        {
            queuedSendMessages[type] = new Queue<IWriteMessage>();
        }

        queuedSendMessages[type].Enqueue(message);
    }
#elif CLIENT
    public void QueueMessage(IWriteMessage message, ClientToServer type)
    {
        if (!queuedSendMessages.ContainsKey(type))
        {
            queuedSendMessages[type] = new Queue<IWriteMessage>();
        }

        queuedSendMessages[type].Enqueue(message);
    }
#endif

    public bool BuildMessage(IWriteMessage packet)
    {
        packet.WriteByte(0);

        int groupCount = 0;

        WriteOnlyMessage allGroupData = new WriteOnlyMessage();

#if SERVER
        foreach ((ServerToClient type, Queue<IWriteMessage> queue) in queuedSendMessages)
#elif CLIENT
        foreach ((ClientToServer type, Queue<IWriteMessage> queue) in queuedSendMessages)
#endif
        {
            if (queue.Count == 0)
            {
                continue;
            }

            WriteOnlyMessage groupData = new WriteOnlyMessage();
            byte amount = 0;

            while (queue.Count > 0)
            {
                IWriteMessage queuedMessage = queue.Peek();

                // Do we have enough space to write this message?
                if (packet.LengthBytes + groupData.LengthBytes + allGroupData.LengthBytes + queuedMessage.LengthBytes > 1024)
                {
                    break;
                }

                queuedMessage = queue.Dequeue();

                groupData.WriteBytes(queuedMessage.Buffer, 0, queuedMessage.LengthBytes);
                amount++;
            }

            if (amount > 0)
            {
                groupCount++;
                allGroupData.WriteByte((byte)type);
                allGroupData.WriteByte(((byte)(amount - 1)));
                allGroupData.WriteUInt16((UInt16)groupData.LengthBytes);
                allGroupData.WriteBytes(groupData.Buffer, 0, groupData.LengthBytes);
            }
        }

        if (groupCount > 0)
        {
            packet.WriteByte((byte)groupCount);
            packet.WriteBytes(allGroupData.Buffer, 0, allGroupData.LengthBytes);
        }

        return groupCount > 0;
    }
}