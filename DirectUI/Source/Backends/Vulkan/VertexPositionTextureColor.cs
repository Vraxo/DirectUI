using System.Numerics;
using System.Runtime.InteropServices;

namespace DirectUI.Backends.Vulkan;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct VertexPositionTextureColor
{
    public Vector2 Position;
    public Vector2 TextureCoordinates;
    public Vector4 Color;

    public VertexPositionTextureColor(Vector2 position, Vector2 textureCoordinates, Vector4 color)
    {
        Position = position;
        TextureCoordinates = textureCoordinates;
        Color = color;
    }
}