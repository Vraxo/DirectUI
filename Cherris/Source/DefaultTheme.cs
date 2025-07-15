using Vortice.Mathematics;

namespace Cherris;
public static class DefaultTheme
{
    public static readonly Color4 White = Colors.White;
    public static readonly Color4 Black = Colors.Black;
    public static readonly Color4 Transparent = Colors.Transparent;
    public static readonly Color4 NormalFill = new Color4(0.25f, 0.25f, 0.3f, 1.0f);
    public static readonly Color4 NormalBorder = new Color4(0.4f, 0.4f, 0.45f, 1.0f);

    public static readonly Color4 HoverFill = new Color4(0.35f, 0.35f, 0.4f, 1.0f);
    public static readonly Color4 HoverBorder = new Color4(0.5f, 0.5f, 0.55f, 1.0f);

    public static readonly Color4 Accent = new Color4(0.2f, 0.4f, 0.8f, 1.0f);    public static readonly Color4 AccentBorder = new Color4(0.3f, 0.5f, 0.9f, 1.0f);
    public static readonly Color4 DisabledFill = new Color4(0.2f, 0.2f, 0.2f, 0.8f);
    public static readonly Color4 DisabledBorder = new Color4(0.3f, 0.3f, 0.3f, 0.8f);
    public static readonly Color4 DisabledText = new Color4(0.5f, 0.5f, 0.5f, 1.0f);

    public static readonly Color4 FocusBorder = Colors.LightSkyBlue;
    public static readonly Color4 Text = Colors.WhiteSmoke;
}