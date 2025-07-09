using System.Runtime.InteropServices;

namespace DirectUI.Drawing;

[StructLayout(LayoutKind.Sequential)]
public struct Color
{
    public byte R;
    public byte G;
    public byte B;
    public byte A;

    public Color(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public Color(float r, float g, float b, float a)
    {
        R = (byte)(r * 255.0f);
        G = (byte)(g * 255.0f);
        B = (byte)(b * 255.0f);
        A = (byte)(a * 255.0f);
    }

    public static implicit operator Raylib_cs.Color(Color color)
    {
        return new Raylib_cs.Color(color.R, color.G, color.B, color.A);
    }

    public static implicit operator Vortice.Mathematics.Color4(Color color)
    {
        return new Vortice.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
    }

    public static implicit operator Color(Vortice.Mathematics.Color4 color)
    {
        return new Color((byte)(color.R * 255), (byte)(color.G * 255), (byte)(color.B * 255), (byte)(color.A * 255));
    }
}