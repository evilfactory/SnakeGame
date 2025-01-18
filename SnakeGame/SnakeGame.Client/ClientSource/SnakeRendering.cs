using System.Numerics;
using MalignEngine;

namespace SnakeGame;

public class SnakeRendering : EntitySystem
{
    [Dependency]
    protected IRenderingService RenderingService = default!;

    public override void OnInitialize()
    {
        EntityRef camera = EntityManager.World.CreateEntity();
        camera.Add(new OrthographicCamera() { IsMain = true, ClearColor = Color.LightSkyBlue, ViewSize = 10 });
    }

    public void DrawBoard(Board board)
    {
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
                        RenderingService.DrawTexture2D(Texture2D.White, position, Vector2.One, Color.Gray, 0f, 0f);
                        break;
                    case TileType.Wall:
                        RenderingService.DrawTexture2D(Texture2D.White, position, Vector2.One, Color.Black, 0f, 0f);
                        break;
                    case TileType.Food:
                        RenderingService.DrawTexture2D(Texture2D.White, position, Vector2.One, Color.Red, 0f, 0f);
                        break;
                    case TileType.SnakeBody:
                        RenderingService.DrawTexture2D(Texture2D.White, position, Vector2.One, Color.GreenYellow, 0f, 0f);
                        break;
                    case TileType.SnakeHead:
                        RenderingService.DrawTexture2D(Texture2D.White, position, Vector2.One, Color.Green, 0f, 0f);
                        break;
                }
            }
        }

        RenderingService.End();
    }

}
