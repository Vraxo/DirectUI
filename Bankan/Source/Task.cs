using System.Text.Json.Serialization;
using DirectUI;
using DirectUI.Drawing;

namespace Bankan;

// --- Board Models ---

public class Task
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;

    [JsonIgnore] // Don't save the DirectUI.Color struct directly
    public Color Color { get; set; } = DefaultTheme.Accent;

    // Use a helper property for JSON serialization to store color as hex string
    [JsonPropertyName("Color")]
    public string ColorHex
    {
        get => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
        set
        {
            if (!string.IsNullOrEmpty(value) && value.StartsWith("#") && value.Length == 7)
            {
                try
                {
                    byte r = Convert.ToByte(value.Substring(1, 2), 16);
                    byte g = Convert.ToByte(value.Substring(3, 2), 16);
                    byte b = Convert.ToByte(value.Substring(5, 2), 16);
                    Color = new Color(r, g, b, 255);
                }
                catch
                {
                    Color = DefaultTheme.Accent; // Fallback
                }
            }
            else
            {
                Color = DefaultTheme.Accent; // Fallback
            }
        }
    }
}
