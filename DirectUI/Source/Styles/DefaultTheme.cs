// DefaultTheme.cs
using DirectUI.Drawing;

namespace DirectUI;

// Defines default colors for the UI theme
public static class DefaultTheme
{
    // --- Theme Color Definitions ---

    private static class Ue5ThemeColors
    {
        // Palette based on Unreal Engine 5 Editor
        public static readonly Color Fill = new Color(42, 42, 42, 255);        // #2A2A2A
        public static readonly Color FillLighter = new Color(58, 58, 58, 255);  // #3A3A3A
        public static readonly Color Border = new Color(21, 21, 21, 255);      // #151515 (Matches window background)

        // --- CHANGE HERE ---
        // Changed from blue to a light gray for a more authentic UE5 feel.
        public static readonly Color Hover = new Color(160, 160, 160, 255);   // #A0A0A0
        // --- END CHANGE ---

        public static readonly Color Accent = new Color(255, 171, 0, 255);     // Orange #FFAB00
        public static readonly Color AccentBorder = new Color(255, 187, 51, 255); // Lighter Orange

        public static readonly Color DisabledFill = new Color(32, 32, 32, (byte)(255 * 0.8f));
        public static readonly Color DisabledBorder = new Color(48, 48, 48, (byte)(255 * 0.8f));
        public static readonly Color DisabledText = new Color(128, 128, 128, 255);

        public static readonly Color Text = new Color(240, 240, 240, 255);     // #F0F0F0
    }

    private static class OriginalColors
    {
        public static readonly Color NormalFill = new Color(64, 64, 77, 255);
        public static readonly Color NormalBorder = new Color(102, 102, 115, 255);
        public static readonly Color HoverFill = new Color(89, 89, 102, 255);
        public static readonly Color HoverBorder = new Color(128, 128, 140, 255);
        public static readonly Color Accent = new Color(51, 102, 204, 255);
        public static readonly Color AccentBorder = new Color(77, 128, 230, 255);
        public static readonly Color DisabledFill = new Color(51, 51, 51, (byte)(255 * 0.8f));
        public static readonly Color DisabledBorder = new Color(77, 77, 77, (byte)(255 * 0.8f));
        public static readonly Color DisabledText = new Color(128, 128, 128, 255);
        public static readonly Color Text = new Color(245, 245, 245, 255); // WhiteSmoke
    }

    private static class UnityEditorThemeColors
    {
        // Palette based on Unity Editor's default dark theme (Pro skin)
        public static readonly Color Fill = new Color(56, 56, 56, 255);         // #383838 - Standard control background
        public static readonly Color Border = new Color(35, 35, 35, 255);        // #232323 - Dark, subtle border
        public static readonly Color SelectionBlue = new Color(62, 95, 150, 255); // #3E5F96 - The iconic blue for selections/presses
        public static readonly Color SelectionBlueBorder = new Color(75, 116, 185, 255); // #4B74B9 - Brighter blue for hover/focus borders

        public static readonly Color DisabledFill = new Color(56, 56, 56, (byte)(255 * 0.8f));
        public static readonly Color DisabledBorder = new Color(32, 32, 32, (byte)(255 * 0.8f));
        public static readonly Color DisabledText = new Color(128, 128, 128, 255);

        public static readonly Color Text = new Color(207, 207, 207, 255);       // #CFCFCF
    }


    // --- Current Active Theme ---
    // Change the assignments here to swap themes.

    // Basic Palette
    public static readonly Color White = new Color(255, 255, 255, 255);
    public static readonly Color Black = new Color(0, 0, 0, 255);
    public static readonly Color Transparent = new Color(0, 0, 0, 0);

    /*
    // --- ACTIVE THEME: UE5 ---
    public static readonly Color NormalFill = Ue5ThemeColors.Fill;
    public static readonly Color NormalBorder = Ue5ThemeColors.Border;
    public static readonly Color HoverFill = Ue5ThemeColors.FillLighter;
    public static readonly Color HoverBorder = Ue5ThemeColors.Hover;
    public static readonly Color Accent = Ue5ThemeColors.Accent;
    public static readonly Color AccentBorder = Ue5ThemeColors.AccentBorder;
    public static readonly Color DisabledFill = Ue5ThemeColors.DisabledFill;
    public static readonly Color DisabledBorder = Ue5ThemeColors.DisabledBorder;
    public static readonly Color DisabledText = Ue5ThemeColors.DisabledText;
    public static readonly Color FocusBorder = Ue5ThemeColors.Hover;
    public static readonly Color Text = Ue5ThemeColors.Text;
    */

    // --- ACTIVE THEME: Unity Editor (Dark) ---
    public static readonly Color NormalFill = UnityEditorThemeColors.Fill;
    public static readonly Color NormalBorder = UnityEditorThemeColors.Border;
    public static readonly Color HoverFill = UnityEditorThemeColors.Fill; // Fill does not change on hover in Unity
    public static readonly Color HoverBorder = UnityEditorThemeColors.SelectionBlueBorder; // Border becomes blue on hover
    public static readonly Color Accent = UnityEditorThemeColors.SelectionBlue; // Accent is the pressed/active color
    public static readonly Color AccentBorder = UnityEditorThemeColors.SelectionBlueBorder;
    public static readonly Color DisabledFill = UnityEditorThemeColors.DisabledFill;
    public static readonly Color DisabledBorder = UnityEditorThemeColors.DisabledBorder;
    public static readonly Color DisabledText = UnityEditorThemeColors.DisabledText;
    public static readonly Color FocusBorder = UnityEditorThemeColors.SelectionBlueBorder; // Focus also uses the blue border
    public static readonly Color Text = UnityEditorThemeColors.Text;


    /*
    // --- BACKUP THEME: Original ---
    // To restore the original theme, comment out the "UE5" block above
    // and uncomment this block below.
    public static readonly Color NormalFill = OriginalColors.NormalFill;
    public static readonly Color NormalBorder = OriginalColors.NormalBorder;
    public static readonly Color HoverFill = OriginalColors.HoverFill;
    public static readonly Color HoverBorder = OriginalColors.HoverBorder;
    public static readonly Color Accent = OriginalColors.Accent;
    public static readonly Color AccentBorder = OriginalColors.AccentBorder;
    public static readonly Color DisabledFill = OriginalColors.DisabledFill;
    public static readonly Color DisabledBorder = OriginalColors.DisabledBorder;
    public static readonly Color DisabledText = OriginalColors.DisabledText;
    public static readonly Color FocusBorder = new Color(135, 206, 250, 255); // LightSkyBlue
    public static readonly Color Text = OriginalColors.Text;
    */
}