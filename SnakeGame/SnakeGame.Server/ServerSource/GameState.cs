using Silk.NET.Maths;

namespace SnakeGame;

public class Snake
{
    public byte PlayerId;

    public List<Vector2D<byte>> Positions = new List<Vector2D<byte>>();

    public PlayerInput Input;
}

public class GameState
{
    public uint Tick { get; set; }

    public Board Board { get; set; }
    public List<Snake> Snakes { get; set; }

    public GameState()
    {
        Snakes = new List<Snake>();
    }

    public GameState Clone()
    {
        var clone = new GameState()
        {
            Tick = Tick,
            Board = Board.Clone(),
            Snakes = Snakes.Select(s => new Snake()
            {
                PlayerId = s.PlayerId,
                Positions = s.Positions.ToList(),
                Input = s.Input
            }).ToList()
        };
        return clone;
    }
}

public abstract class TickEvent
{
    public uint Tick { get; set; }

    public virtual void Perform(GameState state) { }
}

public class BoardResetEvent : TickEvent
{
    public byte Width { get; set; }
    public byte Height { get; set; }

    public override void Perform(GameState state)
    {
        state.Board = new Board(Width, Height);
    }
}

public class BoardSetEvent : TickEvent
{
    public byte X { get; set; }
    public byte Y { get; set; }
    public Tile Tile { get; set; }
    public override void Perform(GameState state)
    {
        state.Board.SetResource(X, Y, Tile);
    }
}

public class SnakeSpawnEvent : TickEvent
{
    public byte PlayerId { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }

    public override void Perform(GameState state)
    {
        state.Snakes.Add(new Snake()
        {
            PlayerId = PlayerId,
            Positions = new List<Vector2D<byte>>()
            {
                new Vector2D<byte>(X, Y),
                new Vector2D<byte>(X, (byte)(Y + 1))
            }
        });

        state.Board.SetResource(X, Y, new Tile() { Type = TileType.SnakeHead, PlayerId = PlayerId });
        state.Board.SetResource(X, (byte)(Y + 1), new Tile() { Type = TileType.SnakeBody, PlayerId = PlayerId });
    }
}

public class SnakeInputEvent : TickEvent
{
    public byte PlayerId { get; set; }
    public PlayerInput Input { get; set; }

    public override void Perform(GameState state)
    {
        var snake = state.Snakes.Where(s => s.PlayerId == PlayerId).First();
        snake.Input = Input;
    }
}

public class SnakeMoveEvent : TickEvent
{
    public byte PlayerId { get; set; }
    public Vector2D<byte> NewHeadPosition { get; set; }
    public bool Grew { get; set; }

    public override void Perform(GameState state)
    {
        var snake = state.Snakes.Where(s => s.PlayerId == PlayerId).First();
        snake.Positions.Insert(0, NewHeadPosition);
        if (!Grew)
        {
            snake.Positions.RemoveAt(snake.Positions.Count - 1);
        }
    }
}

public class SetResourceEvent : TickEvent
{
    public byte X { get; set; }
    public byte Y { get; set; }

    public override void Perform(GameState state)
    {
        state.Board.SetResource(X, Y, new Tile() { Type = TileType.Food });
    }
}

public class SnakeKillEvent : TickEvent
{
    public byte PlayerId { get; set; }

    public override void Perform(GameState state)
    {
        var snake = state.Snakes.Where(s => s.PlayerId == PlayerId).First();
        state.Snakes.Remove(snake);

        foreach (var position in snake.Positions)
        {
            state.Board.SetResource(position.X, position.Y, new Tile() { Type = TileType.Empty });
        }
    }
}