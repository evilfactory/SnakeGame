using System.Numerics;
using MalignEngine;

namespace SnakeGame;

public class SnakeRendering : EntitySystem
{
    [Dependency]
    protected IRenderingService RenderingService = default!;

    private EntityRef camera;

    public override void OnInitialize()
    {
        camera = EntityManager.World.CreateEntity();
        camera.Add(new Transform());
        camera.Add(new OrthographicCamera() { IsMain = true, ClearColor = Color.LightSkyBlue, ViewSize = 10 });
    }

    public void DrawBoard(Board board)
    {
        camera.Get<OrthographicCamera>().ViewSize = Math.Max(board.Width, board.Height) / 1.5f;
        camera.Get<Transform>().Position = new Vector3(board.Width / 2f, board.Height / 2f, 0);

        RenderingService.Begin();

        for (byte x = 0; x < board.Width; x++)
        {
            for (byte y = 0; y < board.Height; y++)
            {
                Vector2 position = new Vector2(x, y);
                Tile tile = board.GetResource(x, y);

                switch (tile.Type)
                {
                    case TileType.Empty:
                        RenderingService.DrawTexture2D(Texture2D.White, position, Vector2.One * 0.9f, Color.Gray, 0f, 0f);
                        break;
                    case TileType.Wall:
                        RenderingService.DrawTexture2D(Texture2D.White, position, Vector2.One * 0.9f, Color.Black, 0f, 0f);
                        break;
                    case TileType.Food:
                        RenderingService.DrawTexture2D(Texture2D.White, position, Vector2.One * 0.9f, Color.Red, 0f, 0f);
                        break;
                    case TileType.SnakeBody:
                        RenderingService.DrawTexture2D(Texture2D.White, position, Vector2.One * 0.9f, Color.GreenYellow, 0f, 0f);
                        break;
                    case TileType.SnakeHead:
                        RenderingService.DrawTexture2D(Texture2D.White, position, Vector2.One * 0.9f, Color.Green, 0f, 0f);
                        break;
                }
            }
        }

        RenderingService.End();
    }

}
