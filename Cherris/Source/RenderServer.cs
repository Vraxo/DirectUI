using Raylib_cs;

namespace Cherris;

public sealed class RenderServer
{
    private static RenderServer? _instance;
    public static RenderServer Instance => _instance ??= new();

    public Camera? Camera;
    public Shader? PostProcessingShader { get; set; }

    private readonly List<DrawCommand> drawCommands = [];
    public RenderTexture2D RenderTexture { get; private set; }

    private RenderServer()
    {
        var mainWin = ApplicationServer.Instance.GetMainAppWindow();
        Vector2 windowSize = mainWin != null ? new Vector2(mainWin.Width, mainWin.Height) : new Vector2(800, 600);
        RenderTexture = Raylib.LoadRenderTexture((int)windowSize.X, (int)windowSize.Y);
    }

    public void WindowSizeChanged(Vector2 newSize)
    {
        Raylib.UnloadRenderTexture(RenderTexture);
        RenderTexture = Raylib.LoadRenderTexture(
            (int)newSize.X,
            (int)newSize.Y);
    }

    public void Process()
    {
        RenderScene();

        BeginShaderMode(PostProcessingShader);
        EndShaderMode();
    }

    public void RenderScene()
    {
        Raylib.BeginTextureMode(RenderTexture);
        BeginCameraMode();
        ProcessDrawCommands();
        EndCameraMode();
        Raylib.EndTextureMode();
    }

    public void Process2()
    {
    }

    public void Submit(Action drawAction, int layer)
    {
        drawCommands.Add(new(drawAction, layer));
    }

    public Vector2 GetScreenToWorld(Vector2 position)
    {
        return Camera is null
            ? position
            : Raylib.GetScreenToWorld2D(position, Camera);
    }

    public Vector2 GetWorldToScreen(Vector2 position)
    {
        return Camera is null
            ? position
            : Raylib.GetWorldToScreen2D(position, Camera);
    }

    public static void BeginScissorMode(Vector2 position, Vector2 size)
    {
        Raylib.BeginScissorMode(
            (int)position.X,
            (int)position.Y,
            (int)size.X,
            (int)size.Y);
    }

    public static void EndScissorMode()
    {
        Raylib.EndScissorMode();
    }

    public void SetCamera(Camera camera)
    {
        Camera = camera;
    }

    private void BeginCameraMode()
    {
        if (Camera is null)
        {
            return;
        }
        Vector2 windowSize = Camera.GetWindowSizeV2();

        Camera2D cam = new()
        {
            Target = Camera.GlobalPosition,
            Offset = windowSize / 2,
            Zoom = Camera.Zoom,
        };

        Raylib.BeginMode2D(cam);
    }

    private void EndCameraMode()
    {
        if (Camera is null)
        {
            return;
        }

        Raylib.EndMode2D();
    }

    public static void BeginShaderMode(Shader? shader)
    {
        if (shader is null)
        {
            return;
        }

        Raylib.BeginShaderMode(shader);
    }

    public static void EndShaderMode()
    {
        Raylib.EndShaderMode();
    }

    private void ProcessDrawCommands()
    {
        foreach (DrawCommand command in drawCommands.OrderBy(c => c.Layer))
        {
            command.DrawAction.Invoke();
        }

        drawCommands.Clear();
    }

    private class DrawCommand(Action drawAction, int layer)
    {
        public Action DrawAction { get; } = drawAction;
        public int Layer { get; } = layer;
    }
}
