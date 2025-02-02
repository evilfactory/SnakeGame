using MalignEngine;
using Silk.NET.Maths;

namespace SnakeGame;

//public class ArenaGameMode : GameMode
//{

//    private float foodSpawnTime = 2f;
//    private DateTime lastFoodSpawn = DateTime.Now;

//    private float movesPerSecond = 10f;
//    private DateTime lastMoveTime = DateTime.Now;

//    public ArenaGameMode()
//    {
//        SnakeServer.ResetBoard(64, 64);

//        for (int x = 5; x < 50; x++)
//        {
//            SnakeServer.SetResource((byte)x, 5, new Tile { Type = TileType.Wall, PlayerId = 0 });
//            SnakeServer.SetResource((byte)x, 8, new Tile { Type = TileType.Wall, PlayerId = 0 });
//        }
//    }

//    public override void Start()
//    {

//    }
//    public override void Simulate()
//    {
//        if (DateTime.Now - lastMoveTime > TimeSpan.FromSeconds(1.0 / movesPerSecond))
//        {
//            lastMoveTime = DateTime.Now;

//            List<Snake> snakes = new List<Snake>(SnakeServer.Snakes);

//            foreach (Snake snake in snakes)
//            {
//                if (snake.Killed) { continue; }

//                MoveSnake(snake);
//            }
//        }

//        if (SnakeServer.Snakes.Count > 0 && DateTime.Now - lastFoodSpawn > TimeSpan.FromSeconds(foodSpawnTime))
//        {
//            lastFoodSpawn = DateTime.Now;

//            byte x = (byte)Random.Shared.Next(0, SnakeServer.Board.Width);
//            byte y = (byte)Random.Shared.Next(0, SnakeServer.Board.Height);

//            if (SnakeServer.Board.GetResource(x, y).Type == TileType.Empty)
//            {
//                SnakeServer.SetResource(x, y, new Tile { Type = TileType.Food, PlayerId = 0 });
//            }
//        }

//        while (respawnQueue.Count > 0)
//        {
//            (NetworkConnection connection, DateTime time) = respawnQueue.Peek();

//            if (DateTime.Now > time)
//            {
//                respawnQueue.Dequeue();
//                if (!connection.IsInvalid)
//                {
//                    SpawnClient(connection);
//                }
//            }
//            else
//            {
//                break;
//            }
//        }
//    }
//    public override void End()
//    {

//    }
//}