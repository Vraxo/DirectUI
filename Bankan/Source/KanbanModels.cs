using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DirectUI;
using DirectUI.Drawing;

namespace Bankan;

// --- Settings Models ---

public enum TaskColorStyle
{
    Border,
    Background
}

public enum TaskTextAlign
{
    Left,
    Center
}

public class KanbanSettings
{
    public TaskColorStyle ColorStyle { get; set; } = TaskColorStyle.Border;
    public TaskTextAlign TextAlign { get; set; } = TaskTextAlign.Left;
}

// --- Board Models ---

public class KanbanTask
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

public class KanbanColumn
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<KanbanTask> Tasks { get; set; } = new();
}

public class KanbanBoard
{
    public List<KanbanColumn> Columns { get; set; } = new();
}