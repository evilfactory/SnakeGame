using MalignEngine;
using System.Numerics;

namespace SnakeGame;

class GameMain
{
    public GameMain()
    {
        Application application = new Application();

        application.AddSystem(new EntityEventSystem());
        application.AddSystem(new EntityManagerService());

        application.AddSystem(new AssetService());
#if CLIENT
        application.AddSystem(new WindowSystem("Snake Game", new Vector2(800, 600)));
        application.AddSystem(new GLRenderingSystem());
        application.AddSystem(new InputSystem());
        application.AddSystem(new CameraSystem());
        application.AddSystem(new SpriteRenderingSystem());
        application.AddSystem(new LightingSystem2D());
        application.AddSystem(new LightingPostProcessingSystem2D());
        application.AddSystem(new AudioSystem());
        application.AddSystem(new FontSystem());
#elif SERVER
        application.AddSystem(new HeadlessUpdateLoop() { UpdateRate = 20 });
#endif
        application.AddSystem(new ParentSystem());
        application.AddSystem(new TransformSystem());
        application.AddSystem(new SceneSystem());
        application.AddSystem(new PhysicsSystem2D());

#if CLIENT
        application.AddSystem(new SnakeClient());
        application.AddSystem(new SnakeRendering());
#elif SERVER
        application.AddSystem(new SnakeServer());
#endif

        application.AddSystem(new SnakeGame());

#if CLIENT
        application.AddSystem(new MainMenu());
#elif SERVER
#endif

#if CLIENT
        application.AddSystem(new ImGuiSystem());
        application.AddSystem(new EditorSystem());
        application.AddSystem(new EditorInspectorSystem());
        application.AddSystem(new EditorPerformanceSystem());
        application.AddSystem(new EditorSceneViewSystem());
        application.AddSystem(new EditorAssetViewer());
        application.AddSystem(new EditorConsole());
        application.AddSystem(new EditorNetworking());
#endif

        application.Run();
    }
}