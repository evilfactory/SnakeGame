using MalignEngine;

namespace SnakeGame;

public class Client
{
    public string Name { get; set; }
    public byte Id => Connection.Id;
    public uint LastTick { get; set; }
    public bool IsSynced { get; set; } = false;
    public NetworkConnection Connection { get; set; }
    public PlayerInput LastInput { get; set; }
}