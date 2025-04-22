// Button.cs
using System;
using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics; // For Rect

namespace DirectUI;

public class Button
{
    // --- Enums ---
    public enum ActionMode { Release, Press }
    public enum ClickBehavior { Left, Right, Both } // Keep this if right-click needed

    // --- Properties from Cherris.Button (Adapted) ---
    public Vector2 Position { get; set; } = Vector2.Zero; // Absolute position
    public Vector2 Size { get; set; } = new(100, 26);
    public Vector2 Origin { get; set; } = Vector2.Zero; // Offset from Position for rotation/scaling pivot

    public string Text { get; set; } = "";
    public Vector2 TextOffset { get; set; } = Vector2.Zero; // Additional offset for text relative to alignment point
    public Alignment TextAlignment { get; set; } = new(HAlignment.Center, VAlignment.Center);
    public ButtonStylePack Themes { get; set; } = new();
    public ActionMode LeftClickActionMode { get; set; } = ActionMode.Release;
    // public ActionMode RightClickActionMode { get; set; } = ActionMode.Release; // If needed
    // public bool StayPressed { get; set; } = false; // Requires state management across frames
    // public bool ClipText { get; set; } = false; // TBD - Requires Text Layout
    public bool AutoWidth { get; set; } = false; // TBD - Requires Text Measurement
    public Vector2 TextMargin { get; set; } = new(10, 5); // Used by AutoWidth
    // public string Ellipsis { get; set; } = "..."; // Used by ClipText
    public ClickBehavior Behavior { get; set; } = ClickBehavior.Left;
    // public Texture? Icon { get; set; } = null; // Icons not supported yet
    // public float IconMargin { get; set; } = 12; // Icons not supported yet

    public bool Disabled { get; set; } = false;
    public object? UserData { get; set; } = null; // Generic data storage

    // --- State (Managed externally by UI.Button logic, but stored here) ---
    public bool IsHovering { get; internal set; } = false;
    public bool IsPressed { get; internal set; } = false; // Combined pressed state (left/right)
    // internal bool pressedLeft = false; // If separate tracking needed
    // internal bool pressedRight = false; // If separate tracking needed

    // --- Events ---
    public event Action<Button>? Clicked; // Simplified single click event
    // public event Action<Button>? LeftClicked;
    // public event Action<Button>? RightClicked;
    public event Action<Button>? MouseEntered;
    public event Action<Button>? MouseExited;

    // --- Calculated Properties ---
    // Calculate bounds based on Position, Size, Origin
    public Rect GlobalBounds
    {
        get
        {
            float left = Position.X - Origin.X;
            float top = Position.Y - Origin.Y;
            float right = left + Size.X;
            float bottom = top + Size.Y;
            return new Rect(left, top, right, bottom);
        }
    }

    // --- Methods ---

    // Method to invoke the Clicked event (called by UI.Button)
    internal void InvokeClick()
    {
        Clicked?.Invoke(this);
    }

    // Method to invoke MouseEntered (called by UI.Button)
    internal void InvokeMouseEnter()
    {
        MouseEntered?.Invoke(this);
    }

    // Method to invoke MouseExited (called by UI.Button)
    internal void InvokeMouseExit()
    {
        MouseExited?.Invoke(this);
    }

    // Method to update the button's style based on current state
    // (called by UI.Button after updating state)
    internal void UpdateStyle()
    {
        Themes.UpdateCurrentStyle(IsHovering, IsPressed, Disabled);
    }

    // Placeholder for text measurement needed by AutoWidth
    internal Vector2 MeasureText(IDWriteFactory dwriteFactory)
    {
        // TODO: Implement text measurement using dwriteFactory.CreateTextLayout
        // This is needed for AutoWidth and potentially complex TextAlignment
        // For now, return an estimate or zero
        if (string.IsNullOrEmpty(Text) || Themes?.Current?.FontName is null)
        {
            return Vector2.Zero;
        }
        // Rough estimate (replace with actual measurement)
        float approxWidth = Text.Length * Themes.Current.FontSize * 0.6f;
        float approxHeight = Themes.Current.FontSize;
        return new Vector2(approxWidth, approxHeight);
    }

    // Placeholder for resizing logic needed by AutoWidth
    internal void PerformAutoWidth(IDWriteFactory dwriteFactory)
    {
        if (!AutoWidth) return;

        Vector2 textSize = MeasureText(dwriteFactory);
        float desiredWidth = textSize.X + TextMargin.X * 2; // Add horizontal margins
        // Only update width, keep existing height? Or use text height + vertical margins?
        // Let's just update width for now.
        Size = new Vector2(Math.Max(Size.X, desiredWidth), Size.Y); // Ensure it doesn't shrink below original size? Or just set it? Let's set it.
        // Size = new Vector2(desiredWidth, Size.Y);
    }
}