using MalignEngine;
using FluentResults;
using System.Numerics;

namespace SnakeGame;

public enum PlayerInput : byte
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3,
    Respawn = 4
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
    SendChatMessage = 5
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

public class BoardReset : NetMessage
{
    public byte Width;
    public byte Height;

    public override void Deserialize(IReadMessage message)
    {
        Width = message.ReadByte();
        Height = message.ReadByte();
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(Width);
        message.WriteByte(Height);
    }

    public override string ToString()
    {
        return $"BoardReset ( Width: {Width}, Height: {Height} )";
    }
}

public class BoardSet : NetMessage
{
    public byte X;
    public byte Y;
    public CellData Data;

    public override void Deserialize(IReadMessage message)
    {
        X = message.ReadByte();
        Y = message.ReadByte();
        Data = new CellData();
        Data.Deserialize(message);
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(X);
        message.WriteByte(Y);
        Data.Serialize(message);
    }

    public override string ToString()
    {
        return $"BoardSet ( X: {X}, Y: {Y}, Data: {Data} )";
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

public class PlayerDisconnected : NetMessage
{
    public byte PlayerId;
    public string Reason;

    public override void Deserialize(IReadMessage message)
    {
        PlayerId = message.ReadByte();
        Reason = message.ReadCharArray();
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(PlayerId);
        message.WriteCharArray(Reason);
    }

    public override string ToString()
    {
        return $"PlayerDisconnected ( PlayerId: {PlayerId}, Reason: {Reason} )";
    }
}

public class PlayerSpawned : NetMessage
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
        return $"PlayerSpawned ( PlayerId: {PlayerId} )";
    }
}

public class PlayerDied : NetMessage
{
    public byte PlayerId;
    public byte RespawnTimeSeconds;

    public override void Deserialize(IReadMessage message)
    {
        PlayerId = message.ReadByte();
        RespawnTimeSeconds = message.ReadByte();
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(PlayerId);
        message.WriteByte(RespawnTimeSeconds);
    }

    public override string ToString()
    {
        return $"PlayerDied ( PlayerId: {PlayerId}, RespawnTimeSeconds: {RespawnTimeSeconds} )";
    }
}

public class PlayerMoved : NetMessage
{
    public byte PlayerId;
    public byte X;
    public byte Y;
    public bool Grew;

    public override void Deserialize(IReadMessage message)
    {
        PlayerId = message.ReadByte();
        X = message.ReadByte();
        Y = message.ReadByte();
        Grew = message.ReadByte() == 1;
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(PlayerId);
        message.WriteByte(X);
        message.WriteByte(Y);
        message.WriteByte((byte)(Grew ? 1 : 0));
    }

    public override string ToString()
    {
        return $"PlayerMoved ( PlayerId: {PlayerId}, NewPosition: ({X},{Y}), Grew: {Grew} )";
    }
}

public class SendChatMessage : NetMessage
{
    public byte PlayerId;
    public string Message;

    public override void Deserialize(IReadMessage message)
    {
        PlayerId = message.ReadByte();
        Message = message.ReadCharArray();
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(PlayerId);
        message.WriteCharArray(Message);
    }

    public override string ToString()
    {
        return $"SendChatMessage ( PlayerId: {PlayerId}, Message: {Message} )";
    }
}

public class CellData : NetMessage
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

    public override string ToString()
    {
        return $"CellData ( Resource: {Resource}, AssociatedPlayerId: {AssociatedPlayerId} )";
    }
}

public class CellDataWithPos : NetMessage
{
    public byte X;
    public byte Y;
    public CellData Data;

    public override void Deserialize(IReadMessage message)
    {
        X = message.ReadByte();
        Y = message.ReadByte();
        Data = new CellData();
        Data.Deserialize(message);
    }

    public override void Serialize(IWriteMessage message)
    {
        message.WriteByte(X);
        message.WriteByte(Y);
        Data.Serialize(message);
    }

    public override string ToString()
    {
        return $"CellDataWithPos ( X: {X}, Y: {Y}, Data: {Data} )";
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
    public void ReadIncoming(IReadMessage message, Action<ClientToServer, IReadMessage> read, ILogger logger)
#elif CLIENT
    public void ReadIncoming(IReadMessage message, Action<ServerToClient, IReadMessage> read, ILogger logger)
#endif
    {
        byte clientTick = message.ReadByte();
        byte groupCount = message.ReadByte();

        logger.LogVerbose($"New packet being read: clientTick = {clientTick}, groupCount = {groupCount}");

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
                logger.LogError($"The message size did not match the skip bytes value, possibly malformed message? connection = {message.Sender}, messageType = {messageType}, messageCount = {messageCount}, skipBytes = {skipBytes}, sizeAfterRead = {sizeAfterRead}, messagePosition = {message.BytePosition}");
            }
        }
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

    public bool BuildMessage(byte gameTick, IWriteMessage packet, ILogger logger)
    {
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
                if (packet.LengthBytes + groupData.LengthBytes + allGroupData.LengthBytes + queuedMessage.LengthBytes > 1024 || amount >= 255)
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

        packet.WriteUInt16((UInt16)allGroupData.LengthBytes);
        packet.WriteByte(gameTick);
        packet.WriteByte((byte)groupCount);
        packet.WriteBytes(allGroupData.Buffer, 0, allGroupData.LengthBytes);

        return groupCount > 0;
    }
}