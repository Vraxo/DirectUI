using Raylib_cs;

namespace Cherris;

public class Camera : Node2D
{
    public float Zoom { get; set; } = 1;

    public void SetAsActive()
    {
        RenderServer.Instance.SetCamera(this);
    }

    public static implicit operator Camera2D(Camera camera)
    {
        Vector2 windowSize = camera.GetWindowSizeV2();

        return new()
        {
            Target = camera.GlobalPosition,
            Offset = windowSize / 2,
            Zoom = camera.Zoom,
        };
    }
}