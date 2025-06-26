// Widgets/Button.cs
namespace DirectUI;

using System;
using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.Direct2D1;
using SharpGen.Runtime;

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
    public ClickBehavior Behavior { get; set; } = Button.ClickBehavior.Left;
    public bool Disabled { get; set; } = false;
    public object? UserData { get; set; } = null;

    public bool IsHovering { get; internal set; } = false;

    public Rect GlobalBounds
    {
        get
        {
            float x = Position.X - Origin.X;
            float y = Position.Y - Origin.Y;
            float width = Math.Max(0, Size.X);
            float height = Math.Max(0, Size.Y);
            Rect calculatedBounds = new(x, y, width, height);

            return calculatedBounds;
        }
    }

    private bool isPressed = false;


    internal bool Update(string id)
    {
        ID2D1HwndRenderTarget? renderTarget = UI.CurrentRenderTarget;
        IDWriteFactory? dwriteFactory = UI.CurrentDWriteFactory;
        InputState input = UI.CurrentInputState;

        if (renderTarget is null || dwriteFactory is null)
        {
            return false;
        }

        // --- Sizing (MUST be done before hit-testing) ---
        // This is now independent of hover/press state and ensures correct bounds.
        PerformAutoWidth(dwriteFactory);

        // --- Input Logic & State Update ---
        bool wasClickedThisFrame = false;
        isPressed = false; // Reset visual pressed state for the frame

        if (Disabled)
        {
            IsHovering = false;
            if (UI.ActivelyPressedElementId == id)
            {
                UI.ClearActivePress(id);
            }
        }
        else // Element is Enabled
        {
            Rect bounds = GlobalBounds; // NOW uses correct size from AutoWidth

            IsHovering = bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(input.MousePosition.X, input.MousePosition.Y);

            if (IsHovering)
            {
                UI.SetPotentialInputTarget(id);
            }

            // Determine relevant actions based on behavior
            bool primaryActionHeld = (Behavior is ClickBehavior.Left or ClickBehavior.Both) && input.IsLeftMouseDown;
            bool primaryActionPressedThisFrame = (Behavior is ClickBehavior.Left or ClickBehavior.Both) && input.WasLeftMousePressedThisFrame;
            // Add Right button logic here if Behavior.Right/Both is implemented fully

            // Handle Release Action
            if (!primaryActionHeld && UI.ActivelyPressedElementId == id)
            {
                if (IsHovering && LeftClickActionMode is ActionMode.Release)
                {
                    wasClickedThisFrame = true;
                }
                UI.ClearActivePress(id); // Clear press regardless of hover on release
            }

            // Handle Press Attempt (potential capture)
            if (primaryActionPressedThisFrame)
            {
                // Can only capture press if hovered, it's the potential target, and no drag was already in progress from a previous frame
                if (IsHovering && UI.PotentialInputTargetId == id && !UI.dragInProgressFromPreviousFrame)
                {
                    // Attempt to capture input - this overwrites previous captors for the frame
                    UI.SetButtonPotentialCaptorForFrame(id);
                }
            }

            // Determine visual pressed state for this frame (is it the element currently held down?)
            isPressed = (UI.ActivelyPressedElementId == id);

        } // End Enabled block

        // --- Final Style Update & Drawing ---
        UpdateStyle(); // Update style based on the now-calculated states

        try
        {
            Rect currentBounds = GlobalBounds; // Bounds are now final for this frame

            if (currentBounds.Width > 0 && currentBounds.Height > 0)
            {
                DrawBackground(renderTarget);
                DrawText(renderTarget, dwriteFactory);
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            // Retained log message for recoverable graphics issue.
            Console.WriteLine($"Render target needs recreation during Button draw: {ex.Message}");
            UI.CleanupResources(); // Clear brushes which are now invalid
            return false; // Cannot continue drawing
        }
        catch (Exception ex)
        {
            // Retained log message for unexpected error.
            Console.WriteLine($"Error drawing button {id}: {ex}");
            return false; // Indicate error
        }


        // --- Final Click Determination (for Press mode) ---
        // Click happens now if mode is Press AND this button was the final input captor for the frame.
        if (!wasClickedThisFrame && LeftClickActionMode is ActionMode.Press && UI.InputCaptorId == id)
        {
            wasClickedThisFrame = true;
        }

        return wasClickedThisFrame;
    }

    internal void UpdateStyle()
    {
        Themes?.UpdateCurrentStyle(IsHovering, isPressed, Disabled);
    }

    internal Vector2 MeasureText(IDWriteFactory dwriteFactory, ButtonStyle styleToUse)
    {
        if (string.IsNullOrEmpty(Text) || styleToUse is null || dwriteFactory is null)
        {
            return Vector2.Zero;
        }

        IDWriteTextFormat? textFormat = null;

        try
        {
            textFormat = dwriteFactory.CreateTextFormat(
                styleToUse.FontName,
                null, // font collection
                styleToUse.FontWeight,
                styleToUse.FontStyle,
                styleToUse.FontStretch,
                styleToUse.FontSize,
                "en-us" // locale
            );

            if (textFormat is null)
            {
                // Retained log message for unexpected font system issue.
                Console.WriteLine("Warning: Failed to create TextFormat for measurement.");
                return Vector2.Zero;
            }

            // Use using for disposable TextLayout
            using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(
                Text,
                textFormat,
                float.MaxValue, // Max width
                float.MaxValue  // Max height
            );

            TextMetrics textMetrics = textLayout.Metrics;

            return new Vector2(textMetrics.WidthIncludingTrailingWhitespace, textMetrics.Height);
        }
        catch (Exception ex)
        {
            // Retained log message for unexpected error.
            Console.WriteLine($"Error measuring text for button: {ex.Message}");
            return Vector2.Zero;
        }
        finally
        {
            textFormat?.Dispose();
        }
    }

    internal void PerformAutoWidth(IDWriteFactory dwriteFactory)
    {
        if (!AutoWidth || dwriteFactory is null || Themes is null)
        {
            return;
        }

        // Always measure using the Normal theme to ensure consistent size across states.
        // This relies on the font properties being set consistently across the style pack.
        Vector2 textSize = MeasureText(dwriteFactory, Themes.Normal);
        float desiredWidth = textSize.X + TextMargin.X * 2; // Add horizontal margins

        // Use a small epsilon to avoid floating point jitter
        if (desiredWidth > 0 && Math.Abs(Size.X - desiredWidth) > 0.1f)
        {
            Size = new Vector2(desiredWidth, Size.Y);
        }
        else if (desiredWidth <= 0 && Size.X != 0)
        {
            // Optional: Handle case where text becomes empty - maybe reset to a MinWidth?
            // For now, do nothing, keeping potentially previous non-zero width.
        }
    }

    private void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        Rect bounds = GlobalBounds;
        ButtonStyle? style = Themes?.Current;

        // Early exit if nothing to draw
        if (style is null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        UI.DrawBoxStyleHelper(renderTarget, new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Width, bounds.Height), style);
    }

    private void DrawText(ID2D1RenderTarget renderTarget, IDWriteFactory dwriteFactory)
    {
        // Early exits for common cases
        if (string.IsNullOrEmpty(Text)) return;
        ButtonStyle? style = Themes?.Current;
        if (style is null) return;

        ID2D1SolidColorBrush textBrush = UI.GetOrCreateBrush(style.FontColor);
        if (textBrush is null) return; // Brush creation failed (error logged in GetOrCreateBrush)

        IDWriteTextFormat? textFormat = null;
        try
        {
            textFormat = dwriteFactory.CreateTextFormat(
                style.FontName,
                null,
                style.FontWeight,
                style.FontStyle,
                style.FontStretch,
                style.FontSize,
                "en-us"
            );

            if (textFormat is null) return; // Should not happen if MeasureText succeeded, but check anyway

            // Apply Alignment
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
            if (bounds.Width <= 0 || bounds.Height <= 0) return; // Avoid drawing in zero-size rect

            // Apply offset to layout rectangle
            Rect layoutRect = bounds;
            layoutRect.Left += TextOffset.X;
            layoutRect.Top += TextOffset.Y;
            // Note: Width/Height remain the button's bounds for centering/alignment calculations within DrawText

            renderTarget.DrawText(
                Text,
                textFormat,
                layoutRect, // The area where text can be drawn
                textBrush,
                DrawTextOptions.Clip // Clip text to the layoutRect automatically
            );
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            // Retained log message for recoverable graphics issue.
            Console.WriteLine($"Button Text Draw failed (RecreateTarget): {ex.Message}.");
            // Don't dispose textFormat here, it might be invalid anyway. Resource cleanup happens externally.
            textFormat = null; // Ensure finally doesn't try to dispose potentially bad pointer
        }
        catch (Exception ex)
        {
            // Retained log message for unexpected error.
            Console.WriteLine($"Error drawing button text: {ex.Message}");
        }
        finally
        {
            textFormat?.Dispose();
        }
    }
}