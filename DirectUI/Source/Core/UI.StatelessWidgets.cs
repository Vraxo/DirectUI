using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    /// <summary>
    /// The core, stateless primitive for drawing and interacting with a button.
    /// This is the single source of truth for all button-like controls.
    /// </summary>
    internal static bool ButtonPrimitive(
        int id,
        Rect bounds,
        string text,
        ButtonStylePack theme,
        bool disabled,
        Alignment textAlignment,
        DirectUI.Button.ActionMode clickMode,
        DirectUI.Button.ClickBehavior clickBehavior,
        Vector2 textOffset)
    {
        var context = Context;
        var state = State;
        var resources = Resources;
        var renderTarget = context.RenderTarget;
        var dwriteFactory = context.DWriteFactory;
        var input = context.InputState;

        // --- State Calculation ---
        bool isFocused = !disabled && state.FocusedElementId == id;
        bool isHovering = !disabled && bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
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


        if (!anyActionHeld && isPressed)
        {
            if (isHovering && clickMode == DirectUI.Button.ActionMode.Release)
            {
                wasClickedThisFrame = true;
            }
            state.ClearActivePress(id);
            isPressed = false;
        }

        if (anyActionPressedThisFrame)
        {
            if (isHovering && state.PotentialInputTargetId == id && !state.DragInProgressFromPreviousFrame)
            {
                state.SetButtonPotentialCaptorForFrame(id);
                state.SetFocus(id);
                isPressed = true;
            }
        }

        if (!wasClickedThisFrame && clickMode == DirectUI.Button.ActionMode.Press && state.InputCaptorId == id)
        {
            wasClickedThisFrame = true;
        }

        // --- Style Resolution ---
        ButtonStyle currentStyle = ResolveButtonStyle(theme, isHovering, isPressed, disabled, isFocused);

        // --- Drawing ---
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            // Draw Background
            resources.DrawBoxStyleHelper(renderTarget, new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Width, bounds.Height), currentStyle);

            // Draw Text
            DrawButtonText(renderTarget, dwriteFactory, resources, bounds, text, currentStyle, textAlignment, textOffset);
        }

        return wasClickedThisFrame;
    }

    /// <summary>
    /// Resolves the final ButtonStyle for the current frame by applying interaction state and style stack overrides.
    /// </summary>
    private static ButtonStyle ResolveButtonStyle(ButtonStylePack theme, bool isHovering, bool isPressed, bool isDisabled, bool isFocused)
    {
        // Determine base style from interaction state
        theme.UpdateCurrentStyle(isHovering, isPressed, isDisabled, isFocused);
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
        else if (isHovering)
        {
            finalStyle.FillColor = GetStyleColor(StyleColor.ButtonHovered, finalStyle.FillColor);
            finalStyle.BorderColor = GetStyleColor(StyleColor.BorderHovered, finalStyle.BorderColor);
        }
        else if (isFocused)
        {
            finalStyle.BorderColor = GetStyleColor(StyleColor.BorderFocused, finalStyle.BorderColor);
        }
        else // Normal
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
    /// Draws the text for a button, using the central text layout cache.
    /// </summary>
    private static void DrawButtonText(
        ID2D1RenderTarget renderTarget,
        IDWriteFactory dwriteFactory,
        UIResources resources,
        Rect bounds,
        string text,
        ButtonStyle style,
        Alignment textAlignment,
        Vector2 textOffset)
    {
        if (string.IsNullOrEmpty(text)) return;

        var textBrush = resources.GetOrCreateBrush(renderTarget, style.FontColor);
        if (textBrush is null) return;

        var layoutKey = new UIResources.TextLayoutCacheKey(text, style, new(bounds.Width, bounds.Height), textAlignment);
        if (!resources.textLayoutCache.TryGetValue(layoutKey, out var textLayout))
        {
            var textFormat = resources.GetOrCreateTextFormat(dwriteFactory, style);
            if (textFormat is null) return;

            textLayout = dwriteFactory.CreateTextLayout(text, textFormat, bounds.Width, bounds.Height);
            textLayout.TextAlignment = textAlignment.Horizontal switch
            {
                HAlignment.Left => Vortice.DirectWrite.TextAlignment.Leading,
                HAlignment.Center => Vortice.DirectWrite.TextAlignment.Center,
                HAlignment.Right => Vortice.DirectWrite.TextAlignment.Trailing,
                _ => Vortice.DirectWrite.TextAlignment.Leading
            };
            textLayout.ParagraphAlignment = textAlignment.Vertical switch
            {
                VAlignment.Top => ParagraphAlignment.Near,
                VAlignment.Center => ParagraphAlignment.Center,
                VAlignment.Bottom => ParagraphAlignment.Far,
                _ => ParagraphAlignment.Near
            };
            resources.textLayoutCache[layoutKey] = textLayout;
        }

        // A small vertical adjustment to compensate for font metrics making text appear slightly too low when using ParagraphAlignment.Center.
        float yOffsetCorrection = (textAlignment.Vertical == VAlignment.Center) ? -1.5f : 0f;

        renderTarget.DrawTextLayout(new Vector2(bounds.X + textOffset.X, bounds.Y + textOffset.Y + yOffsetCorrection), textLayout, textBrush, DrawTextOptions.None);
    }
}