using System.Numerics;
using MalignEngine;

namespace SnakeGame;

public class SnakeSystem : EntitySystem
{
#if CLIENT
    [Dependency]
    protected IRenderingService RenderingService = default!;
#endif


    public override void OnUpdate(float deltaTime)
    {

    }

#if false
    public override void OnDraw(float deltaTime)
    {
        EntityManager.World.Query(EntityManager.World.CreateQuery().WithAll<SnakeComponent, Transform>(), (EntityRef entity, ref SnakeComponent snakeComponent, ref Transform transform) =>
        {
            RenderingService.Begin();
            RenderingService.DrawTexture2D(Texture2D.White, transform.Position.ToVector2(), Vector2.One, 0f);
            
            for (int i = 0; i < snakeComponent.Tail.Count; i++)
            {
                RenderingService.DrawTexture2D(Texture2D.White, transform.Position.ToVector2(), Vector2.One, Color.Red, 0f, 0f);
            }

            RenderingService.End();
        });
    }
#endif
}
