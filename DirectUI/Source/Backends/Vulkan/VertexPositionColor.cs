using System.Numerics;
using System.Runtime.InteropServices;

namespace DirectUI.Backends.Vulkan;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct VertexPositionColor
{
    public Vector2 Position;
    public Vector4 Color;

    public VertexPositionColor(Vector2 position, Vector4 color)
    {
        Position = position;
        Color = color;
    }
}