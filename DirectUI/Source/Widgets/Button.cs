// MODIFIED: Button.cs
// Summary: Full class. Modified DrawBackground to call the shared UI.DrawBoxStyleHelper method. Includes previous fixes.
using System;
using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.Direct2D1;
using SharpGen.Runtime;
// Removed D2D alias as drawing helper is now external

namespace DirectUI;

public class Button
{
    public enum ActionMode { Release, Press }
    public enum ClickBehavior { Left, Right, Both }

    // --- Properties ---
    public Vector2 Position { get; set; } = Vector2.Zero;
    public Vector2 Size { get; set; } = new(84, 28);
    public Vector2 Origin { get; set; } = Vector2.Zero;
    public string Text { get; set; } = "";
    public Vector2 TextOffset { get; set; } = Vector2.Zero;
    public Alignment TextAlignment { get; set; } = new(HAlignment.Center, VAlignment.Center);
    public ButtonStylePack Themes { get; set; } = new();
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
            // Ensure width/height aren't negative for bounds calculation
            float width = Math.Max(0, Size.X);
            float height = Math.Max(0, Size.Y);
            var calculatedBounds = new Rect(x, y, width, height);
            return calculatedBounds;
        }
    }

    // --- Main Update Method ---
    internal bool Update()
    {
        // Get context from UI static class
        ID2D1HwndRenderTarget? renderTarget = UI.CurrentRenderTarget;
        IDWriteFactory? dwriteFactory = UI.CurrentDWriteFactory;
        InputState input = UI.CurrentInputState;

        // Should be valid if called from UI.Button, but check defensively
        if (renderTarget is null || dwriteFactory is null) return false;

        Rect bounds = GlobalBounds; // Calculate bounds based on current Pos/Size/Origin
        bool wasClickedThisFrame = false;
        bool previousHoverState = IsHovering; // Store state before update

        // --- Update Interaction State ---
        if (Disabled)
        {
            IsHovering = false;
            IsPressed = false;
        }
        else
        {
            // Check hover state based on current bounds
            IsHovering = bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(input.MousePosition.X, input.MousePosition.Y);

            // Determine relevant input actions based on ClickBehavior
            bool primaryActionPressed = false;
            bool primaryActionHeld = false;
            bool primaryActionReleasedThisFrame = false; // Check release specifically

            if (Behavior is ClickBehavior.Left or ClickBehavior.Both)
            {
                primaryActionPressed = input.WasLeftMousePressedThisFrame;
                primaryActionHeld = input.IsLeftMouseDown;
                // We infer release if it was held last frame (IsPressed) but not held now
            }
            // TODO: Add Right click logic using separate InputState fields if needed

            // Update IsPressed state based on input and hover
            if (IsHovering && primaryActionPressed)
            {
                IsPressed = true;
            }
            // Check for release only if currently pressed
            else if (IsPressed && !primaryActionHeld)
            {
                // Click event triggered on release *if* still hovering
                if (IsHovering && LeftClickActionMode is ActionMode.Release)
                {
                    wasClickedThisFrame = true;
                }
                IsPressed = false; // Always reset pressed state on release
            }
            // Edge case: ensure IsPressed is false if primary button is not held down
            else if (!primaryActionHeld)
            {
                IsPressed = false;
            }

            // Check for click on press if configured
            if (IsPressed && primaryActionPressed && LeftClickActionMode is ActionMode.Press)
            {
                wasClickedThisFrame = true;
            }
        }

        // Optional: Internal callbacks for state changes (e.g., for sound effects later)
        // if (IsHovering && !previousHoverState) InvokeMouseEnter();
        // else if (!IsHovering && previousHoverState) InvokeMouseExit();

        // --- Update Style & Size ---
        UpdateStyle(); // Update Current theme based on potentially new state
        // Perform auto-width *after* state update but *before* drawing, as it affects bounds
        PerformAutoWidth(dwriteFactory);
        // Recalculate bounds if size changed - GlobalBounds getter does this automatically

        // --- Drawing ---
        try
        {
            // Ensure we have valid bounds after potential auto-width
            if (GlobalBounds.Width > 0 && GlobalBounds.Height > 0)
            {
                DrawBackground(renderTarget);
                DrawText(renderTarget, dwriteFactory);
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Render target needs recreation (Caught in Button drawing): {ex.Message}");
            UI.CleanupResources(); // Request resource cleanup via static UI class
            return false; // Signal failure/need for reinit
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled error during Button drawing: {ex}");
            return false; // Signal failure
        }

        return wasClickedThisFrame;
    }

    // --- Internal Logic Methods ---
    internal void UpdateStyle()
    {
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
            ButtonStyle style = Themes.Current; // Use current style for measurement
            textFormat = dwriteFactory.CreateTextFormat(
                style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us"
            );

            if (textFormat is null)
            {
                Console.WriteLine("Warning: Failed to create TextFormat for measurement.");
                return Vector2.Zero;
            }

            // Use MaxValue for layout width/height to get the intrinsic size
            using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(Text, textFormat, float.MaxValue, float.MaxValue);
            // Get metrics which include width and height
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
        if (!AutoWidth || dwriteFactory is null)
        {
            return;
        }

        Vector2 textSize = MeasureText(dwriteFactory);
        // Ensure margin is applied correctly
        float desiredWidth = textSize.X + TextMargin.X * 2; // Add margin to both sides

        // Only update if the desired width is positive and significantly different
        if (desiredWidth > 0 && Math.Abs(Size.X - desiredWidth) > 0.1f)
        {
            Size = new Vector2(desiredWidth, Size.Y);
        }
        else if (desiredWidth <= 0 && Size.X != 0) // Handle case where text becomes empty
        {
            // Optionally reset to a minimum width or keep current height
            // For now, let's just ensure it doesn't have the old width if text is gone
            // This behavior might need refinement based on desired UX.
            // Setting width to 0 might cause issues, perhaps use a MinWidth?
            // Let's keep the existing Size.Y but set width to 0 if text is gone/empty.
            // Size = new Vector2(0, Size.Y); // Be careful with zero width
        }
    }

    // --- Drawing Methods ---

    // MODIFIED DrawBackground
    private void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        // Get potentially updated bounds after PerformAutoWidth
        Rect bounds = GlobalBounds;
        // Use the current theme state set by UpdateStyle()
        ButtonStyle? style = Themes?.Current;

        // Check validity before calling helper
        if (style is null || bounds.Width <= 0 || bounds.Height <= 0) return;

        // Call the shared helper method now located in UI class
        UI.DrawBoxStyleHelper(renderTarget, new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Width, bounds.Height), style);
    }

    private void DrawText(ID2D1RenderTarget renderTarget, IDWriteFactory dwriteFactory)
    {
        // Check if text exists and style is available
        if (string.IsNullOrEmpty(Text)) return;
        ButtonStyle? style = Themes?.Current;
        if (style is null) return;

        // Get brush using the shared helper
        ID2D1SolidColorBrush textBrush = UI.GetOrCreateBrush(style.FontColor);
        if (textBrush is null) return; // Brush creation failed

        IDWriteTextFormat? textFormat = null;
        try
        {
            // Create format based on current style
            textFormat = dwriteFactory.CreateTextFormat(
                style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us"
            );
            if (textFormat is null)
            {
                Console.WriteLine("Error: Failed to create TextFormat for drawing.");
                return;
            }

            // Apply alignment from TextAlignment property
            textFormat.TextAlignment = TextAlignment.Horizontal switch
            {
                HAlignment.Left => Vortice.DirectWrite.TextAlignment.Leading,
                HAlignment.Center => Vortice.DirectWrite.TextAlignment.Center,
                HAlignment.Right => Vortice.DirectWrite.TextAlignment.Trailing,
                _ => Vortice.DirectWrite.TextAlignment.Leading // Default
            };
            textFormat.ParagraphAlignment = TextAlignment.Vertical switch
            {
                VAlignment.Top => Vortice.DirectWrite.ParagraphAlignment.Near,
                VAlignment.Center => Vortice.DirectWrite.ParagraphAlignment.Center,
                VAlignment.Bottom => Vortice.DirectWrite.ParagraphAlignment.Far,
                _ => Vortice.DirectWrite.ParagraphAlignment.Near // Default
            };

            // Get potentially updated bounds after PerformAutoWidth
            Rect bounds = GlobalBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return; // Check bounds again

            // Apply text offset to the layout rectangle used for drawing
            // Note: TextMargin is used in PerformAutoWidth, TextOffset shifts the drawing rect
            Rect layoutRect = bounds;
            layoutRect.Left += TextOffset.X;
            layoutRect.Top += TextOffset.Y;
            // It's generally okay not to adjust Right/Bottom for DrawText offset,
            // as DrawText respects the layoutRect size and alignment settings.
            // layoutRect.Right += TextOffset.X;
            // layoutRect.Bottom += TextOffset.Y;

            // Draw the text within the calculated layout rectangle
            // Use Options = Clip to ensure text doesn't spill if offset pushes it out
            renderTarget.DrawText(Text, textFormat, layoutRect, textBrush, DrawTextOptions.Clip);
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Text Draw failed (RecreateTarget): {ex.Message}. External cleanup needed.");
            // Don't try to dispose textFormat here as context might be invalid
            textFormat = null; // Prevent dispose attempt in finally
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error drawing button text: {ex.Message}");
        }
        finally
        {
            // Dispose format only if successfully created and no RecreateTarget occurred
            textFormat?.Dispose();
        }
    }
}