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

    public Board Clone()
    {
        var clone = new Board(Width, Height);
        for (byte x = 0; x < Width; x++)
        {
            for (byte y = 0; y < Height; y++)
            {
                clone.SetResource(x, y, GetResource(x, y));
            }
        }
        return clone;
    }
}