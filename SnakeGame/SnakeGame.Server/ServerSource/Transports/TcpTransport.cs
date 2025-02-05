using System.Net;
using System.Net.Sockets;
using MalignEngine;

namespace SnakeGame;

partial class TcpTransport : Transport
{
    private class QueuedSendMessage
    {
        public IWriteMessage Message { get; set; }
        public NetworkConnection Connection { get; set; }
    }

    private class QueuedReceiveMessage
    {
        public IReadMessage Message { get; set; }
        public NetworkConnection Connection { get; set; }
    }

    private TcpListener server;

    private Queue<QueuedSendMessage> sendQueue = new Queue<QueuedSendMessage>();
    private Queue<QueuedReceiveMessage> receiveQueue = new Queue<QueuedReceiveMessage>();

    Dictionary<byte, TcpClient> clients = new Dictionary<byte, TcpClient>();

    public override void SendToClient(IWriteMessage message, NetworkConnection connection, PacketChannel packetChannel = PacketChannel.Reliable)
    {
        if (!clients.ContainsKey(connection.Id))
        {
            throw new Exception("Client not found");
        }

        sendQueue.Enqueue(new QueuedSendMessage
        {
            Message = message,
            Connection = connection
        });
    }

    public override void Listen(int port)
    {
        server = new TcpListener(IPAddress.Any, port);
        server.Start();

        AcceptClients();
    }

    public override void Shutdown()
    {
        server.Stop();
        server = null;
    }

    public override void Update()
    {
        if (server == null) { return; }

        while (sendQueue.Count > 0)
        {
            QueuedSendMessage message = sendQueue.Dequeue();
            NetworkConnection connection = message.Connection;

            if (connection.IsInvalid)
            {
                Logger.LogWarning($"Dropped packet for invalid connection {connection}");
                continue;
            }

            try
            {
                NetworkStream stream = clients[connection.Id].GetStream();
                stream.Write(message.Message.Buffer, 0, message.Message.LengthBytes);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Dropped packet to connection {connection}, error = {ex.Message}");
                continue;
            }
        }

        while (receiveQueue.Count > 0)
        {
            QueuedReceiveMessage message = receiveQueue.Dequeue();
            if (message.Connection.IsInvalid)
            {
                Logger.LogWarning($"Ignored packet from invalid connection {message.Connection}");
                continue;
            }

            OnMessageReceived?.Invoke(message.Message);
        }
    }


    public override void DisconnectClient(NetworkConnection connection, DisconnectReason reason)
    {
        if (connection.IsInvalid || !clients.ContainsKey(connection.Id))
        {
            return;
        }

        TcpClient client = clients[connection.Id];

        client.Close();
        clients.Remove(connection.Id);
        connection.IsInvalid = true;

        OnClientDisconnected?.Invoke(connection, reason);
    }

    private async void AcceptClients()
    {
        while (server != null)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            byte id = CreateId();

            if (id == 0)
            {
                client.Close();
                return;
            }

            var connection = new NetworkConnection(id);
            connection.IsInvalid = false;
            OnClientConnected?.Invoke(connection);
            clients.Add(connection.Id, client);

            HandleClient(client, connection);
        }
    }

    private async void HandleClient(TcpClient client, NetworkConnection connection)
    {
        NetworkStream stream = client.GetStream();
        byte[] headerBuffer = new byte[2];
        byte[] bodyBuffer = new byte[1300 - headerBuffer.Length];

        try
        {
            while (true)
            {
                await stream.ReadExactlyAsync(headerBuffer, 0, headerBuffer.Length);

                ReadOnlyMessage headerMessage = new ReadOnlyMessage(headerBuffer, false, 0, headerBuffer.Length);
                UInt16 bodyLength = (UInt16)(headerMessage.ReadUInt16() + 2);

                await stream.ReadExactlyAsync(bodyBuffer, 0, bodyLength);

                ReadOnlyMessage bodyMessage = new ReadOnlyMessage(bodyBuffer, false, 0, bodyLength);
                bodyMessage.Sender = connection;

                receiveQueue.Enqueue(new QueuedReceiveMessage
                {
                    Message = bodyMessage,
                    Connection = connection
                });
            }
        }
        catch (Exception exception)
        {
            Logger.LogVerbose(exception.ToString());
            DisconnectClient(connection, DisconnectReason.Unknown);
        }
    }

    private byte CreateId()
    {
        // Find a free id
        for (byte i = 1; i < byte.MaxValue; i++)
        {
            if (!clients.ContainsKey(i))
            {
                return i;
            }
        }

        return 0;
    }
}
