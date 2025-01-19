using System.Net;
using System.Net.Sockets;
using MalignEngine;

namespace SnakeGame;

partial class TcpTransport : Transport
{
    private TcpClient client;

    private Queue<IWriteMessage> sendQueue = new Queue<IWriteMessage>();
    private Queue<IReadMessage> receiveQueue = new Queue<IReadMessage>();

    public override void SendToServer(IWriteMessage message, PacketChannel packetChannel = PacketChannel.Reliable)
    {
        if (client == null)
        {
            throw new Exception("Client not connected");
        }

        sendQueue.Enqueue(message);
    }

    public override void Connect(IPEndPoint endpoint)
    {
        sendQueue.Clear();
        receiveQueue.Clear();

        try
        {
            client = new TcpClient();
            client.Connect(endpoint);
        }
        catch
        {
            Disconnect(DisconnectReason.FailedToConnect);
            return;
        }

        OnConnected?.Invoke();

        ReceiveDataAsync();
    }

    private async void ReceiveDataAsync()
    {
        NetworkStream stream = client.GetStream();
        byte[] headerBuffer = new byte[2];
        byte[] bodyBuffer = new byte[1300 - headerBuffer.Length];
        int bytesRead;

        try
        {
            while (true)
            {
                await stream.ReadExactlyAsync(headerBuffer, 0, headerBuffer.Length);

                ReadOnlyMessage headerMessage = new ReadOnlyMessage(headerBuffer, false, 0, headerBuffer.Length);
                UInt16 bodyLength = (UInt16)(headerMessage.ReadUInt16() + 2);

                await stream.ReadExactlyAsync(bodyBuffer, 0, bodyLength);

                ReadOnlyMessage bodyMessage = new ReadOnlyMessage(bodyBuffer, false, 0, bodyLength);

                receiveQueue.Enqueue(bodyMessage);
            }
        }
        catch
        {
            Disconnect(DisconnectReason.Unknown);
        }
    }

    public override void Disconnect(DisconnectReason reason)
    {
        client.Close();
        client = null;

        OnDisconnected?.Invoke(reason);
    }

    public override void Update()
    {
        if (client == null || !client.Connected) { return; }

        while (sendQueue.Count > 0)
        {
            IWriteMessage message = sendQueue.Dequeue();

            NetworkStream stream = client.GetStream();
            stream.Write(message.Buffer, 0, message.LengthBytes);
        }

        while (receiveQueue.Count > 0)
        {
            OnMessageReceived?.Invoke(receiveQueue.Dequeue());
        }
    }
}
