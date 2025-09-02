using System.Numerics;
using DirectUI.Drawing;
using Vortice.Direct2D1; // Still used for AntialiasMode enum
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    /// <summary>
    /// The core, stateless primitive for drawing and interacting with a button.
    /// This is the single source of truth for all button-like controls.
    /// </summary>
    internal static bool DrawButtonPrimitive(
        int id,
        Vortice.Mathematics.Rect bounds,
        string text,
        ButtonStylePack theme,
        bool disabled,
        Alignment textAlignment,
        DirectUI.Button.ActionMode clickMode,
        DirectUI.Button.ClickBehavior clickBehavior,
        Vector2 textOffset,
        bool isActive = false,
        int layer = 1)
    {
        var context = UI.Context;
        var state = UI.State;
        var renderer = context.Renderer;
        var textService = context.TextService;
        var input = context.InputState;

        // --- State Calculation ---
        bool isFocused = !disabled && state.FocusedElementId == id;
        bool isHovering = !disabled && bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        if (isHovering)
        {
            // A widget is only truly hovered if the mouse is within its bounds
            // AND within the current layout clip rect. This prevents interaction
            // with elements that are drawn outside their container's clipped area.
            var currentClip = context.Layout.GetCurrentClipRect();
            if (!currentClip.Contains(input.MousePosition.X, input.MousePosition.Y))
            {
                isHovering = false;
            }
        }
        if (isHovering)
        {
            state.SetPotentialInputTarget(id);
        }

        // --- Click Detection ---
        bool wasClickedThisFrame = false;
        bool isPressed = state.ActivelyPressedElementId == id;

        bool primaryActionPressedThisFrame = (clickBehavior is DirectUI.Button.ClickBehavior.Left or DirectUI.Button.ClickBehavior.Both) && input.WasLeftMousePressedThisFrame;
        bool secondaryActionPressedThisFrame = (clickBehavior is DirectUI.Button.ClickBehavior.Right or DirectUI.Button.ClickBehavior.Both) && input.WasRightMousePressedThisFrame;
        bool anyActionPressedThisFrame = primaryActionPressedThisFrame || secondaryActionPressedThisFrame;

        bool primaryActionHeld = (clickBehavior is DirectUI.Button.ClickBehavior.Left or DirectUI.Button.ClickBehavior.Both) && input.IsLeftMouseDown;
        bool secondaryActionHeld = (clickBehavior is DirectUI.Button.ClickBehavior.Right or DirectUI.Button.ClickBehavior.Both) && input.IsRightMouseDown;
        bool anyActionHeld = primaryActionHeld || secondaryActionHeld;


        // Handle MOUSE UP
        if (!anyActionHeld && isPressed)
        {
            if (isHovering && clickMode == DirectUI.Button.ActionMode.Release)
            {
                // A click happens on release IF this button was the one that initially captured the input.
                if (state.InputCaptorId == id)
                {
                    wasClickedThisFrame = true;
                    Console.WriteLine($"[CLICK-ACTION] ID: {id}, Text: '{text}'");
                }
            }
            state.ClearActivePress(id);
            isPressed = false;
        }

        // Handle MOUSE DOWN
        if (anyActionPressedThisFrame && isHovering && state.PotentialInputTargetId == id && !state.DragInProgressFromPreviousFrame)
        {
            // Any button, regardless of mode, registers its intent to be clicked.
            state.ClickCaptureServer.RequestCapture(id, layer);
            // It also tries to become the "active" element for immediate visual feedback.
            if (state.TrySetActivePress(id, layer))
            {
                state.SetFocus(id);
            }
        }

        // For 'Press' mode, the click is triggered if this button was the winner from the *previous* frame's resolution.
        if (clickMode == DirectUI.Button.ActionMode.Press && state.PressActionWinnerId == id)
        {
            wasClickedThisFrame = true;
            Console.WriteLine($"[CLICK-ACTION] ID: {id}, Text: '{text}'");
        }

        // --- Style Resolution ---
        // Re-check `isPressed` for correct visual state, as it might have changed above.
        isPressed = state.ActivelyPressedElementId == id;
        ButtonStyle currentStyle = ResolveButtonStylePrimitive(theme, isHovering, isPressed, disabled, isFocused, isActive);

        // --- Drawing ---
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            // Draw Background
            renderer.DrawBox(bounds, currentStyle);

            // Draw Text
            DrawTextPrimitive(bounds, text, currentStyle, textAlignment, textOffset);
        }

        return wasClickedThisFrame;
    }

    /// <summary>
    /// Resolves the final ButtonStyle for the current frame by applying interaction state and style stack overrides.
    /// </summary>
    internal static ButtonStyle ResolveButtonStylePrimitive(ButtonStylePack theme, bool isHovering, bool isPressed, bool isDisabled, bool isFocused, bool isActive)
    {
        // Determine base style from interaction state
        theme.UpdateCurrentStyle(isHovering, isPressed, isDisabled, isFocused, isActive);
        ButtonStyle baseStyle = theme.Current;

        // Create a temporary, modifiable copy for this frame to apply style stack overrides
        var finalStyle = new ButtonStyle
        {
            FillColor = baseStyle.FillColor,
            BorderColor = baseStyle.BorderColor,
            FontColor = baseStyle.FontColor,
            BorderLengthTop = baseStyle.BorderLengthTop,
            BorderLengthRight = baseStyle.BorderLengthRight,
            BorderLengthBottom = baseStyle.BorderLengthBottom,
            BorderLengthLeft = baseStyle.BorderLengthLeft,
            Roundness = baseStyle.Roundness,
            FontName = baseStyle.FontName,
            FontSize = baseStyle.FontSize,
            FontWeight = baseStyle.FontWeight,
            FontStyle = baseStyle.FontStyle,
            FontStretch = baseStyle.FontStretch
        };

        // Override with values from the style stack if they exist
        if (isDisabled)
        {
            finalStyle.FillColor = GetStyleColor(StyleColor.ButtonDisabled, finalStyle.FillColor);
            finalStyle.BorderColor = GetStyleColor(StyleColor.BorderDisabled, finalStyle.BorderColor);
            finalStyle.FontColor = GetStyleColor(StyleColor.TextDisabled, finalStyle.FontColor);
        }
        else if (isPressed)
        {
            finalStyle.FillColor = GetStyleColor(StyleColor.ButtonPressed, finalStyle.FillColor);
            finalStyle.BorderColor = GetStyleColor(StyleColor.BorderPressed, finalStyle.BorderColor);
        }
        else if (isHovering && !isActive) // Don't apply button hover if it's an active tab/button
        {
            finalStyle.FillColor = GetStyleColor(StyleColor.ButtonHovered, finalStyle.FillColor);
            finalStyle.BorderColor = GetStyleColor(StyleColor.BorderHovered, finalStyle.BorderColor);
        }
        else if (isFocused && !isActive) // Don't apply focus border if it's an active tab/button
        {
            finalStyle.BorderColor = GetStyleColor(StyleColor.BorderFocused, finalStyle.BorderColor);
        }
        else if (!isActive) // Normal
        {
            finalStyle.FillColor = GetStyleColor(StyleColor.Button, finalStyle.FillColor);
            finalStyle.BorderColor = GetStyleColor(StyleColor.Border, finalStyle.BorderColor);
        }

        if (!isDisabled)
        {
            finalStyle.FontColor = GetStyleColor(StyleColor.Text, finalStyle.FontColor);
        }

        finalStyle.Roundness = GetStyleVar(StyleVar.FrameRounding, finalStyle.Roundness);
        finalStyle.BorderLength = GetStyleVar(StyleVar.FrameBorderSize, finalStyle.BorderLength);

        return finalStyle;
    }

    /// <summary>
    /// The single, unified primitive for drawing cached text within a bounding box.
    /// </summary>
    internal static void DrawTextPrimitive(
        Vortice.Mathematics.Rect bounds,
        string text,
        ButtonStyle style,
        Alignment textAlignment,
        Vector2 textOffset)
    {
        if (string.IsNullOrEmpty(text)) return;

        var renderer = UI.Context.Renderer;

        // Calculate a clean drawing origin without backend-specific corrections.
        Vector2 drawOrigin = new Vector2(bounds.X + textOffset.X, bounds.Y + textOffset.Y);

        // Call the backend-agnostic DrawText method on the renderer.
        // The renderer is now responsible for any specific positional adjustments.
        renderer.DrawText(drawOrigin, text, style, textAlignment, new Vector2(bounds.Width, bounds.Height), style.FontColor);
    }
}