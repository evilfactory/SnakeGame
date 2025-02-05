using MalignEngine;
using Silk.NET.Maths;

namespace SnakeGame;

public abstract class GameMode
{
    public SnakeServer Server; // TODO: redesign this

    protected Simulation Sim;

    public abstract void Start(Simulation sim);

    public abstract void Update();
    public abstract void End();
}

public class BaseGameMode : GameMode, IReceiveClientInput
{
    public override void Start(Simulation simulation)
    {
        Sim = simulation;

        Sim.PushEvent(new BoardResetEvent() { Width = 32, Height = 32 });
    }

    public override void Update()
    {
        if (Sim.CurrentTick % 2 == 0)
        {
            for (int i = 0; i < Sim.State.Snakes.Count; i++)
            {
                MoveSnake(Sim.State.Snakes[i]);
            }
        }

        if (Sim.CurrentTick % 100 == 0)
        {
            byte x = (byte)Random.Shared.Next(0, Sim.State.Board.Width);
            byte y = (byte)Random.Shared.Next(0, Sim.State.Board.Height);
            if (Sim.State.Board.GetResource(x, y).Type == TileType.Empty)
            {
                Sim.PushEvent(new BoardSetEvent() { X = x, Y = y, Tile = new Tile() { Type = TileType.Food, PlayerId = 0 } });
            }
        }

        if (Sim.CurrentTick % 200 == 0)
        {
            byte x = (byte)Random.Shared.Next(0, Sim.State.Board.Width);
            byte y = (byte)Random.Shared.Next(0, Sim.State.Board.Height);
            if (Sim.State.Board.GetResource(x, y).Type == TileType.Empty)
            {
                Sim.PushEvent(new BoardSetEvent() { X = x, Y = y, Tile = new Tile() { Type = TileType.Wall, PlayerId = 0 } });
            }
        }

        foreach (Client client in Server.Clients)
        {
            bool spawned = Sim.State.Snakes.Exists(s => s.PlayerId == client.Id);
            if (!spawned)
            {
                Server.SendToClient(new WriteOnlyMessage(), ServerToClient.RespawnAllowed, client.Connection);
            }
        }
    }

    public override void End()
    {

    }

    public virtual void ReceiveInput(Client client, PlayerInput input)
    {
        if (input == PlayerInput.Respawn)
        {
            if (Sim.State.Snakes.Exists(s => s.PlayerId == client.Id)) { return; }

            Sim.PushEvent(new SnakeSpawnEvent() { PlayerId = client.Id, X = 15, Y = 15 });
        }
        else
        {
            if (!Sim.State.Snakes.Exists(s => s.PlayerId == client.Id)) { return; }

            Sim.PushEvent(new SnakeInputEvent() { PlayerId = client.Id, Input = input });
        }
    }

    private void KillSnakeAndSpawnFood(Snake snake)
    {
        Sim.PushEvent(new SnakeKillEvent() { PlayerId = snake.PlayerId });

        for (int i = 0; i < snake.Positions.Count; i++)
        {
            if (Random.Shared.Next(0, 100) > 50)
            {
                Sim.PushEvent(new BoardSetEvent() { X = snake.Positions[i].X, Y = snake.Positions[i].Y, Tile = new Tile() { Type = TileType.Empty, PlayerId = 0 } });
            }
            else
            {
                Sim.PushEvent(new BoardSetEvent() { X = snake.Positions[i].X, Y = snake.Positions[i].Y, Tile = new Tile() { Type = TileType.Food, PlayerId = 0 } });
            }
        }
    }

    private void MoveSnake(Snake snake)
    {
        Vector2D<byte> newHeadPosition = snake.Positions[0];

        switch (snake.Input)
        {
            case PlayerInput.Up:
                newHeadPosition.Y--;
                break;
            case PlayerInput.Down:
                newHeadPosition.Y++;
                break;
            case PlayerInput.Left:
                newHeadPosition.X--;
                break;
            case PlayerInput.Right:
                newHeadPosition.X++;
                break;
        }

        newHeadPosition.X = (byte)(newHeadPosition.X % Sim.State.Board.Width);
        newHeadPosition.Y = (byte)(newHeadPosition.Y % Sim.State.Board.Height);


        bool grow = false;

        // Check if the head position will end up in food
        Tile tile = Sim.State.Board.GetResource(newHeadPosition.X, newHeadPosition.Y);
        if (tile.Type == TileType.Food)
        {
            grow = true;
        }
        else if (tile.Type == TileType.Wall || tile.Type == TileType.SnakeBody || tile.Type == TileType.SnakeHead)
        {
            KillSnakeAndSpawnFood(snake);
            return;
        }

        Sim.PushEvent(new SnakeMoveEvent() { PlayerId = snake.PlayerId, NewHeadPosition = newHeadPosition, Grew = grow });

        Sim.PushEvent(new BoardSetEvent() { X = newHeadPosition.X, Y = newHeadPosition.Y, Tile = new Tile() { Type = TileType.SnakeHead, PlayerId = snake.PlayerId } });
        Sim.PushEvent(new BoardSetEvent() { X = snake.Positions[0].X, Y = snake.Positions[0].Y, Tile = new Tile() { Type = TileType.SnakeBody, PlayerId = snake.PlayerId } });
        
        if (!grow)
        {
            Sim.PushEvent(new BoardSetEvent() { X = snake.Positions[snake.Positions.Count - 1].X, Y = snake.Positions[snake.Positions.Count - 1].Y, Tile = new Tile() { Type = TileType.Empty, PlayerId = 0 } });
        }
    }
}