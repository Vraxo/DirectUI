// DefaultTheme.cs
using Vortice.Mathematics;

namespace DirectUI;

// Defines default colors for the UI theme
public static class DefaultTheme
{
    // --- Theme Color Definitions ---

    private static class Ue5ThemeColors
    {
        // Palette based on Unreal Engine 5 Editor
        public static readonly Color4 Fill = new Color4(42 / 255f, 42 / 255f, 42 / 255f, 1.0f);        // #2A2A2A
        public static readonly Color4 FillLighter = new Color4(58 / 255f, 58 / 255f, 58 / 255f, 1.0f);  // #3A3A3A
        public static readonly Color4 Border = new Color4(21 / 255f, 21 / 255f, 21 / 255f, 1.0f);      // #151515 (Matches window background)

        // --- CHANGE HERE ---
        // Changed from blue to a light gray for a more authentic UE5 feel.
        public static readonly Color4 Hover = new Color4(160 / 255f, 160 / 255f, 160 / 255f, 1.0f);   // #A0A0A0
        // --- END CHANGE ---

        public static readonly Color4 Accent = new Color4(255 / 255f, 171 / 255f, 0 / 255f, 1.0f);     // Orange #FFAB00
        public static readonly Color4 AccentBorder = new Color4(255 / 255f, 187 / 255f, 51 / 255f, 1.0f); // Lighter Orange

        public static readonly Color4 DisabledFill = new Color4(32 / 255f, 32 / 255f, 32 / 255f, 0.8f);
        public static readonly Color4 DisabledBorder = new Color4(48 / 255f, 48 / 255f, 48 / 255f, 0.8f);
        public static readonly Color4 DisabledText = new Color4(128 / 255f, 128 / 255f, 128 / 255f, 1.0f);

        public static readonly Color4 Text = new Color4(240 / 255f, 240 / 255f, 240 / 255f, 1.0f);     // #F0F0F0
    }

    private static class OriginalColors
    {
        public static readonly Color4 NormalFill = new Color4(0.25f, 0.25f, 0.3f, 1.0f);
        public static readonly Color4 NormalBorder = new Color4(0.4f, 0.4f, 0.45f, 1.0f);
        public static readonly Color4 HoverFill = new Color4(0.35f, 0.35f, 0.4f, 1.0f);
        public static readonly Color4 HoverBorder = new Color4(0.5f, 0.5f, 0.55f, 1.0f);
        public static readonly Color4 Accent = new Color4(0.2f, 0.4f, 0.8f, 1.0f);
        public static readonly Color4 AccentBorder = new Color4(0.3f, 0.5f, 0.9f, 1.0f);
        public static readonly Color4 DisabledFill = new Color4(0.2f, 0.2f, 0.2f, 0.8f);
        public static readonly Color4 DisabledBorder = new Color4(0.3f, 0.3f, 0.3f, 0.8f);
        public static readonly Color4 DisabledText = new Color4(0.5f, 0.5f, 0.5f, 1.0f);
        public static readonly Color4 Text = Colors.WhiteSmoke;
    }


    // --- Current Active Theme ---
    // Change the assignments here to swap themes.

    // Basic Palette
    public static readonly Color4 White = Colors.White;
    public static readonly Color4 Black = Colors.Black;
    public static readonly Color4 Transparent = Colors.Transparent;

    // --- ACTIVE THEME: UE5 ---
    public static readonly Color4 NormalFill = Ue5ThemeColors.Fill;
    public static readonly Color4 NormalBorder = Ue5ThemeColors.Border;
    public static readonly Color4 HoverFill = Ue5ThemeColors.FillLighter;    // Subtle gray change for hover fill
    public static readonly Color4 HoverBorder = Ue5ThemeColors.Hover;        // Light gray border on hover
    public static readonly Color4 Accent = Ue5ThemeColors.Accent;            // Orange accent for pressed
    public static readonly Color4 AccentBorder = Ue5ThemeColors.AccentBorder;
    public static readonly Color4 DisabledFill = Ue5ThemeColors.DisabledFill;
    public static readonly Color4 DisabledBorder = Ue5ThemeColors.DisabledBorder;
    public static readonly Color4 DisabledText = Ue5ThemeColors.DisabledText;
    public static readonly Color4 FocusBorder = Ue5ThemeColors.Hover;        // Use gray for focus
    public static readonly Color4 Text = Ue5ThemeColors.Text;

    /*
    // --- BACKUP THEME: Original ---
    // To restore the original theme, comment out the "UE5" block above
    // and uncomment this block below.
    public static readonly Color4 NormalFill = OriginalColors.NormalFill;
    public static readonly Color4 NormalBorder = OriginalColors.NormalBorder;
    public static readonly Color4 HoverFill = OriginalColors.HoverFill;
    public static readonly Color4 HoverBorder = OriginalColors.HoverBorder;
    public static readonly Color4 Accent = OriginalColors.Accent;
    public static readonly Color4 AccentBorder = OriginalColors.AccentBorder;
    public static readonly Color4 DisabledFill = OriginalColors.DisabledFill;
    public static readonly Color4 DisabledBorder = OriginalColors.DisabledBorder;
    public static readonly Color4 DisabledText = OriginalColors.DisabledText;
    public static readonly Color4 FocusBorder = Colors.LightSkyBlue;
    public static readonly Color4 Text = OriginalColors.Text;
    */
}