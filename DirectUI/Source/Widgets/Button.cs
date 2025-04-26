// Summary: Final version based on simplified logic. Update checks InputCaptorId to determine Press mode click. Press handling calls universal SetPotentialCaptorForFrame.
using System;
using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.Direct2D1;
using SharpGen.Runtime;

namespace DirectUI;

public class Button
{
    public enum ActionMode { Release, Press }
    public enum ClickBehavior { Left, Right, Both }

    // --- Properties ---
    public Vector2 Position { get; set; } = Vector2.Zero;
    public Vector2 Size { get; set; } = new(84, 28);
    public Vector2 Origin { get; set; } = Vector2.Zero; // Offset from Position for alignment/drawing origin
    public string Text { get; set; } = "";
    public Vector2 TextOffset { get; set; } = Vector2.Zero; // Additional offset for text rendering within bounds
    public Alignment TextAlignment { get; set; } = new(HAlignment.Center, VAlignment.Center); // Text alignment within bounds
    public ButtonStylePack Themes { get; set; } = new(); // Theme pack for different states
    public ActionMode LeftClickActionMode { get; set; } = ActionMode.Release; // When the click action triggers
    public bool AutoWidth { get; set; } = false; // Automatically adjust width based on text content
    public Vector2 TextMargin { get; set; } = new(10, 5); // Margin used for AutoWidth calculation (X=horizontal, Y=vertical)
    public ClickBehavior Behavior { get; set; } = Button.ClickBehavior.Left; // Which mouse button(s) trigger actions
    public bool Disabled { get; set; } = false; // If the button is non-interactive
    public object? UserData { get; set; } = null; // Optional user data associated with the button

    // --- Internal State ---
    public bool IsHovering { get; internal set; } = false; // True if mouse is currently over the button bounds
    private bool isPressed = false; // Tracks the *visual* pressed state, based on global active state

    // Calculates the screen-space bounds of the button
    public Rect GlobalBounds
    {
        get
        {
            float x = Position.X - Origin.X;
            float y = Position.Y - Origin.Y;
            float width = Math.Max(0, Size.X); // Ensure non-negative width
            float height = Math.Max(0, Size.Y); // Ensure non-negative height
            var calculatedBounds = new Rect(x, y, width, height);
            return calculatedBounds;
        }
    }

    // --- Main Update Method ---
    // Called once per frame by UI.Button
    internal bool Update(string id)
    {
        ID2D1HwndRenderTarget? renderTarget = UI.CurrentRenderTarget;
        IDWriteFactory? dwriteFactory = UI.CurrentDWriteFactory;
        InputState input = UI.CurrentInputState;

        // Ensure necessary context exists
        if (renderTarget is null || dwriteFactory is null) return false;

        bool wasClickedThisFrame = false; // Return value: true if a click action occurred this frame
        // Reset visual state assumption, determined later based on global state
        isPressed = false;

        if (Disabled)
        {
            // If disabled, ensure no interaction state and clear global active state if it matches this ID
            IsHovering = false;
            if (UI.ActivelyPressedElementId == id) UI.ClearActivePress(id);
        }
        else // Button is enabled
        {
            // --- 1. Check Hover State & Set Potential Target ---
            Rect bounds = GlobalBounds;
            IsHovering = bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
            if (IsHovering)
            {
                // If hovered, declare this button as the current potential target for input
                UI.SetPotentialInputTarget(id);
            }

            // --- Relevant Input States ---
            bool primaryActionHeld = (Behavior is ClickBehavior.Left or ClickBehavior.Both) && input.IsLeftMouseDown;
            bool primaryActionPressedThisFrame = (Behavior is ClickBehavior.Left or ClickBehavior.Both) && input.WasLeftMousePressedThisFrame;

            // --- 2. Handle Mouse Release Logic ---
            // Check if the globally active element is this button AND the mouse is now released
            if (UI.ActivelyPressedElementId == id && !primaryActionHeld)
            {
                // If click mode is Release and mouse released while hovering, trigger click
                if (IsHovering && LeftClickActionMode is ActionMode.Release)
                {
                    wasClickedThisFrame = true;
                }
                // Release the global active state
                UI.ClearActivePress(id);
            }

            // --- 3. Handle Mouse Press Attempt (Overwriting Logic) ---
            // Check if the primary mouse button was pressed *this frame*
            if (primaryActionPressedThisFrame)
            {
                // Check interaction conditions:
                // a) Button is hovered
                // b) Button is the current potential target (last element processed under cursor)
                // c) No drag operation was already in progress from a *previous* frame
                if (IsHovering && UI.PotentialInputTargetId == id && !UI.dragInProgressFromPreviousFrame)
                {
                    // Conditions met: Attempt to become the input captor for this frame.
                    // This call overwrites any previous potential captor set earlier this frame.
                    UI.SetPotentialCaptorForFrame(id);
                    // Note: The actual click for 'Press' mode is determined *after* all elements
                    // are processed, using the final 'UI.InputCaptorId'.
                }
            }

            // --- 4. Determine CURRENT Visual Pressed State for drawing ---
            // Button appears visually pressed if it is the currently globally active element.
            isPressed = (UI.ActivelyPressedElementId == id);

        } // End !Disabled block

        // --- Update Visual Style & Perform Auto-Sizing ---
        UpdateStyle(); // Update internal theme based on IsHovering, isPressed, Disabled
        PerformAutoWidth(dwriteFactory); // Adjust size if AutoWidth is enabled

        // --- Drawing ---
        try
        {
            // Draw only if bounds are valid
            if (GlobalBounds.Width > 0 && GlobalBounds.Height > 0)
            {
                DrawBackground(renderTarget);
                DrawText(renderTarget, dwriteFactory);
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            // Handle device lost/recreation needed error
            Console.WriteLine($"Render target needs recreation during Button draw: {ex.Message}");
            UI.CleanupResources(); // Signal need for cleanup
            return false; // Prevent further processing this frame
        }
        catch (Exception ex)
        {
            // Log other drawing errors
            Console.WriteLine($"Error drawing button '{id}': {ex}");
            return false;
        }


        // --- Final Click Determination for Return Value ---
        // If click hasn't already been triggered by Release mode (wasClickedThisFrame is false),
        // check if mode is Press AND if this button was the FINAL recorded input captor for the frame.
        if (!wasClickedThisFrame && LeftClickActionMode is ActionMode.Press && UI.InputCaptorId == id)
        {
            // Conditions met for Press mode click
            wasClickedThisFrame = true;
        }

        // Return true if either Release or Press mode conditions were met this frame
        return wasClickedThisFrame;
    }


    // --- Internal Logic Methods ---

    // Updates the 'Current' style in the Themes pack based on interaction state
    internal void UpdateStyle()
    {
        Themes?.UpdateCurrentStyle(IsHovering, isPressed, Disabled);
    }

    // Measures the text size based on the current theme's font settings
    internal Vector2 MeasureText(IDWriteFactory dwriteFactory)
    {
        if (string.IsNullOrEmpty(Text) || Themes?.Current?.FontName is null || dwriteFactory is null) return Vector2.Zero;

        IDWriteTextFormat? textFormat = null;
        try
        {
            ButtonStyle style = Themes.Current;
            // Create a temporary text format for measurement
            textFormat = dwriteFactory.CreateTextFormat(style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us");
            if (textFormat is null)
            {
                Console.WriteLine($"Warning: Failed to create TextFormat for measurement (Button: {Text}).");
                return Vector2.Zero;
            }
            // Create a text layout with max dimensions to get the actual needed size
            using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(Text, textFormat, float.MaxValue, float.MaxValue);
            TextMetrics textMetrics = textLayout.Metrics;
            // Return the measured width and height
            return new Vector2(textMetrics.WidthIncludingTrailingWhitespace, textMetrics.Height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error measuring button text '{Text}': {ex.Message}");
            return Vector2.Zero;
        }
        finally
        {
            // Dispose the temporary format object
            textFormat?.Dispose();
        }
    }

    // Adjusts the button's Size.X property if AutoWidth is enabled
    internal void PerformAutoWidth(IDWriteFactory dwriteFactory)
    {
        if (!AutoWidth || dwriteFactory is null) return; // Only proceed if AutoWidth is true

        Vector2 textSize = MeasureText(dwriteFactory);
        // Calculate desired width based on text size and horizontal margin
        float desiredWidth = textSize.X + TextMargin.X * 2;

        // Update Size.X if desired width is positive and different from current width
        if (desiredWidth > 0 && Math.Abs(Size.X - desiredWidth) > 0.1f) // Use a small tolerance for float comparison
        {
            Size = new Vector2(desiredWidth, Size.Y); // Keep original height
        }
        else if (desiredWidth <= 0 && Size.X != 0)
        {
            // Optional: Handle case where text becomes empty or unmeasurable
            // Could reset to a default MinWidth or keep current width.
            // Setting Size.X = 0 might cause issues if not handled elsewhere.
            // For now, we do nothing, keeping the previous non-zero width.
        }
    }

    // --- Drawing Methods ---

    // Draws the button's background/border using the current theme style
    private void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        Rect bounds = GlobalBounds;
        ButtonStyle? style = Themes?.Current;
        // Ensure style and valid bounds exist
        if (style is null || bounds.Width <= 0 || bounds.Height <= 0) return;
        // Use the shared drawing helper
        UI.DrawBoxStyleHelper(renderTarget, new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Width, bounds.Height), style);
    }

    // Draws the button's text using the current theme style and alignment
    private void DrawText(ID2D1RenderTarget renderTarget, IDWriteFactory dwriteFactory)
    {
        // Ensure text exists and style is available
        if (string.IsNullOrEmpty(Text)) return;
        ButtonStyle? style = Themes?.Current;
        if (style is null) return;

        // Get the text brush (cached or created)
        ID2D1SolidColorBrush textBrush = UI.GetOrCreateBrush(style.FontColor);
        if (textBrush is null) return; // Failed to get brush

        IDWriteTextFormat? textFormat = null;
        try
        {
            // Create the text format based on current style
            textFormat = dwriteFactory.CreateTextFormat(style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us");
            if (textFormat is null) return; // Failed to create format

            // Apply text alignment settings from the button's TextAlignment property
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

            // Define the layout rectangle, applying the TextOffset
            Rect bounds = GlobalBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return; // Don't draw if no valid area
            Rect layoutRect = bounds;
            layoutRect.Left += TextOffset.X;
            layoutRect.Top += TextOffset.Y;
            // Adjust bounds slightly if needed, though DrawText clips anyway
            // layoutRect.Width = Math.Max(0, layoutRect.Width - TextOffset.X);
            // layoutRect.Height = Math.Max(0, layoutRect.Height - TextOffset.Y);

            // Draw the text within the layout rectangle, clipping if necessary
            renderTarget.DrawText(Text, textFormat, layoutRect, textBrush, DrawTextOptions.Clip);
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            // Handle device lost during text drawing
            Console.WriteLine($"Text Draw failed (RecreateTarget): {ex.Message}.");
            textFormat = null; // Ensure format isn't disposed twice if error occurred before dispose
            UI.CleanupResources(); // Request resource cleanup
        }
        catch (Exception ex)
        {
            // Log other text drawing errors
            Console.WriteLine($"Error drawing button text '{Text}': {ex.Message}");
        }
        finally
        {
            // Dispose the text format object
            textFormat?.Dispose();
        }
    }
}