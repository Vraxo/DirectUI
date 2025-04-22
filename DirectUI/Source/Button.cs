// Button.cs
using System;
using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.Direct2D1;
using SharpGen.Runtime;

namespace DirectUI;

public class Button
{
    public enum ActionMode
    {
        Release,
        Press
    }

    public enum ClickBehavior
    {
        Left,
        Right,
        Both
    }

    public Vector2 Position { get; set; } = Vector2.Zero;
    public Vector2 Size { get; set; } = new(84, 28);
    public Vector2 Origin { get; set; } = Vector2.Zero;
    public string Text { get; set; } = "";
    public Vector2 TextOffset { get; set; } = Vector2.Zero;
    public Alignment TextAlignment { get; set; } = new(HAlignment.Center, VAlignment.Center);
    public ButtonStylePack Themes { get; set; } = new(); // Ensure initialized
    public ActionMode LeftClickActionMode { get; set; } = ActionMode.Release;
    public bool AutoWidth { get; set; } = false;
    public Vector2 TextMargin { get; set; } = new(10, 5);
    public ClickBehavior Behavior { get; set; } = ClickBehavior.Left;
    public bool Disabled { get; set; } = false;
    public object? UserData { get; set; } = null;

    public bool IsHovering { get; internal set; } = false;
    public bool IsPressed { get; internal set; } = false;

    public Rect GlobalBounds
    {
        get
        {
            float x = Position.X - Origin.X;
            float y = Position.Y - Origin.Y;
            float width = Size.X;
            float height = Size.Y;
            var calculatedBounds = new Rect(x, y, width, height);
            return calculatedBounds;
        }
    }

    // REMOVED public events: Clicked, MouseEntered, MouseExited

    // Update now returns true if clicked this frame, false otherwise
    // This method is called internally by UI.Button
    internal bool Update()
    {
        ID2D1HwndRenderTarget? renderTarget = UI.CurrentRenderTarget;
        IDWriteFactory? dwriteFactory = UI.CurrentDWriteFactory;
        InputState input = UI.CurrentInputState;

        if (renderTarget is null || dwriteFactory is null)
        {
            // This case is already checked in UI.Button, but added defensively
            Console.WriteLine("Error: Button.Update called outside of UI context.");
            return false;
        }

        Rect bounds = GlobalBounds;
        bool wasClickedThisFrame = false;
        bool previousHoverState = IsHovering;

        if (Disabled)
        {
            IsHovering = false;
            IsPressed = false;
        }
        else
        {
            IsHovering = bounds.Contains(input.MousePosition.X, input.MousePosition.Y);

            if (Behavior is Button.ClickBehavior.Left or Button.ClickBehavior.Both)
            {
                if (IsHovering && input.WasLeftMousePressedThisFrame)
                {
                    IsPressed = true;
                    if (LeftClickActionMode is Button.ActionMode.Press)
                    {
                        // Internal flag set, UI.Button will return true
                        wasClickedThisFrame = true;
                    }
                }
                else if (IsPressed && !input.IsLeftMouseDown)
                {
                    if (IsHovering && LeftClickActionMode is Button.ActionMode.Release)
                    {
                        // Internal flag set, UI.Button will return true
                        wasClickedThisFrame = true;
                    }
                    IsPressed = false; // Reset pressed state regardless of hover
                }
                else if (!input.IsLeftMouseDown)
                {
                    // Ensure pressed state is cleared if mouse is released outside
                    IsPressed = false;
                }
            }
            // Add similar logic here for Right mouse button if Behavior allows
        }

        // Optional: Handle internal state changes (like hover effects) if needed,
        // even though public events are removed.
        if (IsHovering && !previousHoverState)
        {
            InvokeMouseEnter(); // Still potentially useful for internal logic/theming
        }
        else if (!IsHovering && previousHoverState)
        {
            InvokeMouseExit(); // Still potentially useful for internal logic/theming
        }

        UpdateStyle(); // Update Current theme based on state
        PerformAutoWidth(dwriteFactory); // Adjust size if needed

        try
        {
            DrawBackground(renderTarget);
            DrawText(renderTarget, dwriteFactory);
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Render target needs recreation (Caught SharpGenException during Button drawing): {ex.Message}");
            UI.CleanupResources(); // Request resource cleanup
            // Need a way to signal the main loop to reinitialize, returning false might suffice for now
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled error during Button drawing: {ex}");
            // Depending on severity, might want to signal failure
            return false;
        }

        // Return the click status determined earlier
        return wasClickedThisFrame;
    }

    // Kept internal methods for potential future use or complex internal logic
    internal void InvokeClick() { /* No longer needed externally */ }
    internal void InvokeMouseEnter() { /* Can be used for internal effects */ }
    internal void InvokeMouseExit() { /* Can be used for internal effects */ }

    internal void UpdateStyle()
    {
        // Themes might be null if UI.Button wasn't called correctly, add null check
        Themes?.UpdateCurrentStyle(IsHovering, IsPressed, Disabled);
    }

    internal Vector2 MeasureText(IDWriteFactory dwriteFactory)
    {
        if (string.IsNullOrEmpty(Text) || Themes?.Current?.FontName is null || dwriteFactory is null)
        {
            return Vector2.Zero;
        }

        IDWriteTextFormat? textFormat = null;
        try
        {
            ButtonStyle style = Themes.Current;
            textFormat = dwriteFactory.CreateTextFormat(
                style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us"
            );

            if (textFormat is null)
            {
                Console.WriteLine("Warning: Failed to create TextFormat for measurement.");
                return Vector2.Zero;
            }

            using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(Text, textFormat, float.MaxValue, float.MaxValue);
            TextMetrics textMetrics = textLayout.Metrics;
            return new Vector2(textMetrics.WidthIncludingTrailingWhitespace, textMetrics.Height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error measuring text: {ex.Message}");
            return Vector2.Zero;
        }
        finally
        {
            textFormat?.Dispose();
        }
    }

    internal void PerformAutoWidth(IDWriteFactory dwriteFactory)
    {
        if (!AutoWidth)
        {
            return;
        }

        Vector2 textSize = MeasureText(dwriteFactory);
        float desiredWidth = textSize.X + TextMargin.X * 2;
        if (desiredWidth > 0)
        {
            // Only update width, keep height
            Size = new Vector2(desiredWidth, Size.Y);
        }
    }

    private void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        Rect bounds = GlobalBounds;
        ButtonStyle? style = Themes?.Current; // Null check

        if (style is null) return; // Cannot draw without style

        ID2D1SolidColorBrush fillBrush = UI.GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush borderBrush = UI.GetOrCreateBrush(style.BorderColor);

        // Check if brushes are valid (GetOrCreateBrush returns null on failure)
        bool canFill = style.FillColor.A > 0 && fillBrush is not null;
        bool canDrawBorder = style.BorderThickness > 0 && style.BorderColor.A > 0 && borderBrush is not null;

        // Ensure bounds are valid before drawing
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (style.Roundness > 0.0f)
        {
            // Ensure radii are not negative
            var radiusX = Math.Max(0, bounds.Width * style.Roundness * 0.5f);
            var radiusY = Math.Max(0, bounds.Height * style.Roundness * 0.5f);
            // Vortice uses System.Drawing.RectangleF for RoundedRectangle ctor overload
            RoundedRectangle roundedRect = new((System.Drawing.RectangleF)bounds, radiusX, radiusY);

            if (canFill)
            {
                renderTarget.FillRoundedRectangle(roundedRect, fillBrush);
            }
            if (canDrawBorder)
            {
                renderTarget.DrawRoundedRectangle(roundedRect, borderBrush, style.BorderThickness);
            }
        }
        else
        {
            if (canFill)
            {
                renderTarget.FillRectangle(bounds, fillBrush);
            }
            if (canDrawBorder)
            {
                renderTarget.DrawRectangle(bounds, borderBrush, style.BorderThickness);
            }
        }
    }

    private void DrawText(ID2D1RenderTarget renderTarget, IDWriteFactory dwriteFactory)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        ButtonStyle? style = Themes?.Current; // Null check
        if (style is null) return;

        ID2D1SolidColorBrush textBrush = UI.GetOrCreateBrush(style.FontColor);
        if (textBrush is null) // Check if brush creation succeeded
        {
            return;
        }

        IDWriteTextFormat? textFormat = null;
        try
        {
            textFormat = dwriteFactory.CreateTextFormat(
                style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us"
            );

            if (textFormat is null)
            {
                Console.WriteLine("Error: Failed to create TextFormat for drawing.");
                return;
            }

            textFormat.TextAlignment = TextAlignment.Horizontal switch
            {
                HAlignment.Left => Vortice.DirectWrite.TextAlignment.Leading,
                HAlignment.Center => Vortice.DirectWrite.TextAlignment.Center,
                HAlignment.Right => Vortice.DirectWrite.TextAlignment.Trailing,
                _ => Vortice.DirectWrite.TextAlignment.Leading
            };
            textFormat.ParagraphAlignment = TextAlignment.Vertical switch
            {
                VAlignment.Top => Vortice.DirectWrite.ParagraphAlignment.Near,
                VAlignment.Center => Vortice.DirectWrite.ParagraphAlignment.Center,
                VAlignment.Bottom => Vortice.DirectWrite.ParagraphAlignment.Far,
                _ => Vortice.DirectWrite.ParagraphAlignment.Near
            };

            Rect bounds = GlobalBounds;
            // Ensure valid bounds for layout
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            Rect layoutRect = bounds;
            layoutRect.Left += TextOffset.X;
            layoutRect.Top += TextOffset.Y;
            // Adjust right/bottom based on offset too if needed, though DrawText usually clips
            // layoutRect.Right += TextOffset.X; // Not typically needed
            // layoutRect.Bottom += TextOffset.Y;

            renderTarget.DrawText(Text, textFormat, layoutRect, textBrush);
        }
        finally
        {
            textFormat?.Dispose();
        }
    }
}