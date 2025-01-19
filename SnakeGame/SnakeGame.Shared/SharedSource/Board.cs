using MalignEngine;
using Silk.NET.Maths;

namespace SnakeGame;

public enum TileType : byte
{
    Empty = 0,
    Wall = 1,
    Food = 2,
    SnakeBody = 3,
    SnakeHead = 4
}


public class Tile
{
    public TileType Type;
    public byte PlayerId;
}

public class Board
{
    public byte Width { get; private set; }
    public byte Height { get; private set; }

    private Tile[,] grid;

    public Board(byte width, byte height)
    {
        Width = width;
        Height = height;

        grid = new Tile[width, height];
    }

    public void SetResource(byte x, byte y, Tile resource)
    {
        grid[x, y] = resource;
    }

    public Tile GetResource(byte x, byte y)
    {
        return grid[x, y] ?? new Tile() { PlayerId = 0, Type = TileType.Empty };
    }
}

public class Snake
{
    public byte PlayerId;

    public Vector2D<byte> HeadPosition;
    public List<Vector2D<byte>> BodyPositions = new List<Vector2D<byte>>();

    public bool Killed = false;

    public PlayerInput Input;
}
