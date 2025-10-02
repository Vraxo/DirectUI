// Entire file content here
using System;
using System.Numerics;
using DirectUI.Animation;
using DirectUI.Drawing;
using Vortice.Direct2D1; // Still used for AntialiasMode enum
using Vortice.Mathematics;

namespace DirectUI;

public enum ClickResult { None, Click, DoubleClick }

public static partial class UI
{
    /// <summary>
    /// The core, stateless primitive for drawing and interacting with a button.
    /// This is the single source of truth for all button-like controls.
    /// </summary>
    public static ClickResult DrawButtonPrimitive(
        int id,
        Vortice.Mathematics.Rect bounds,
        string text,
        ButtonStylePack theme,
        bool disabled,
        Alignment textAlignment,
        DirectUI.Button.ActionMode clickMode,
        DirectUI.Button.ClickBehavior clickBehavior,
        Vector2 textOffset,
        out Rect renderBounds,
        out ButtonStyle animatedStyle,
        bool isActive = false,
        int layer = 1,
        AnimationInfo? animation = null)
    {
        var context = UI.Context;
        var scale = context.UIScale;
        var state = UI.State;
        var renderer = context.Renderer;
        var textService = context.TextService;
        var input = context.InputState;

        ClickResult clickResult = ClickResult.None;
        renderBounds = bounds; // Initialize out parameter

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
                    clickResult = state.RegisterClick(id);
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

        // For 'Press' mode, the click is triggered if this button was the winner from the *previous* frame's resolution,
        // AND it is still the element that has captured input (i.e., no higher layer element has stolen the press).
        if (clickMode == DirectUI.Button.ActionMode.Press && state.PressActionWinnerId == id && state.InputCaptorId == id)
        {
            clickResult = state.RegisterClick(id);
        }

        // --- Style Resolution ---
        // Re-check `isPressed` for correct visual state, as it might have changed above.
        isPressed = state.ActivelyPressedElementId == id;
        ButtonStyle targetStyle = ResolveButtonStylePrimitive(theme, isHovering, isPressed, disabled, isFocused, isActive);
        Vector2 animatedScale = targetStyle.Scale;

        // --- Animation Resolution ---
        // Hierarchy of animation preference:
        // 1. Explicitly passed 'animation' parameter.
        // 2. Animation defined on the target style state (e.g., Hover style).
        // 3. Animation defined on the parent ButtonStylePack.
        AnimationInfo? finalAnimation = animation ?? targetStyle.Animation ?? theme.Animation;


        if (finalAnimation is not null && !disabled)
        {
            var animManager = state.AnimationManager;
            var currentTime = context.TotalTime;

            var fillColor = animManager.GetOrAnimate(HashCode.Combine(id, "FillColor"), targetStyle.FillColor, currentTime, finalAnimation.Duration, finalAnimation.Easing);
            var borderColor = animManager.GetOrAnimate(HashCode.Combine(id, "BorderColor"), targetStyle.BorderColor, currentTime, finalAnimation.Duration, finalAnimation.Easing);
            var borderLength = animManager.GetOrAnimate(HashCode.Combine(id, "BorderLength"), targetStyle.BorderLength, currentTime, finalAnimation.Duration, finalAnimation.Easing);
            animatedScale = animManager.GetOrAnimate(HashCode.Combine(id, "Scale"), targetStyle.Scale, currentTime, finalAnimation.Duration, finalAnimation.Easing);

            animatedStyle = new ButtonStyle(targetStyle)
            {
                FillColor = fillColor,
                BorderColor = borderColor,
                BorderLength = borderLength,
                Scale = animatedScale
            };
        }
        else
        {
            animatedStyle = targetStyle;
        }


        // Create a physical style for rendering with scaled properties.
        var renderStyle = new ButtonStyle(animatedStyle)
        {
            FontSize = animatedStyle.FontSize * scale,
            BorderLengthTop = animatedStyle.BorderLengthTop * scale,
            BorderLengthRight = animatedStyle.BorderLengthRight * scale,
            BorderLengthBottom = animatedStyle.BorderLengthBottom * scale,
            BorderLengthLeft = animatedStyle.BorderLengthLeft * scale
        };

        // --- Drawing ---
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            // Calculate visual bounds based on animated scale, centered within the layout bounds.
            Vector2 center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            float renderWidth = bounds.Width * animatedScale.X;
            float renderHeight = bounds.Height * animatedScale.Y;
            renderBounds = new Rect(
                center.X - renderWidth / 2f,
                center.Y - renderHeight / 2f,
                renderWidth,
                renderHeight
            );

            // Draw Background using the style with scaled border lengths and the animated render bounds.
            renderer.DrawBox(renderBounds, renderStyle);

            // Draw Text using the style with scaled font size and the animated render bounds.
            DrawTextPrimitive(renderBounds, text, renderStyle, textAlignment, textOffset);
        }

        return clickResult;
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
            FontStretch = baseStyle.FontStretch,
            Scale = baseStyle.Scale,
            Animation = baseStyle.Animation // Copy animation info
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
    public static void DrawTextPrimitive(
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