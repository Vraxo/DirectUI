using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    internal static bool StatelessButton(
        int id,
        Rect bounds,
        string text,
        ButtonStylePack stylePack,
        Alignment textAlignment,
        DirectUI.Button.ActionMode clickActionMode,
        bool autoWidth = false,
        Vector2? textMargin = null,
        Vector2 textOffset = default)
    {
        InputState input = CurrentInputState;
        ID2D1HwndRenderTarget renderTarget = CurrentRenderTarget!;
        IDWriteFactory dwriteFactory = CurrentDWriteFactory!;

        Vector2 finalSize = new(bounds.Width, bounds.Height);

        if (autoWidth)
        {
            Vector2 measuredSize = MeasureText(dwriteFactory, text, stylePack.Normal);
            Vector2 margin = textMargin ?? new Vector2(10, 5);
            finalSize.X = measuredSize.X + margin.X * 2;
        }

        Rect finalBounds = new(bounds.X, bounds.Y, finalSize.X, finalSize.Y);

        bool isHovering = finalBounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        bool isPressed = ActivelyPressedElementId == id;
        bool wasClickedThisFrame = false;

        if (isHovering)
        {
            SetPotentialInputTarget(id);
        }

        if (!input.IsLeftMouseDown && isPressed)
        {
            if (isHovering && clickActionMode == DirectUI.Button.ActionMode.Release)
            {
                wasClickedThisFrame = true;
            }

            ClearActivePress(id);
            isPressed = false;
        }

        if (input.WasLeftMousePressedThisFrame && isHovering && PotentialInputTargetId == id && !dragInProgressFromPreviousFrame)
        {
            SetButtonPotentialCaptorForFrame(id);
            isPressed = true;
        }

        if (!wasClickedThisFrame && clickActionMode == DirectUI.Button.ActionMode.Press && InputCaptorId == id)
        {
            wasClickedThisFrame = true;
        }

        stylePack.UpdateCurrentStyle(isHovering, isPressed, false);
        ButtonStyle currentStyle = stylePack.Current;

        DrawBoxStyleHelper(renderTarget, new(finalBounds.X, finalBounds.Y), new(finalBounds.Width, finalBounds.Height), currentStyle);

        ID2D1SolidColorBrush textBrush = GetOrCreateBrush(currentStyle.FontColor);
        IDWriteTextFormat? textFormat = GetOrCreateTextFormat(currentStyle);
        
        if (textBrush is not null && textFormat is not null && !string.IsNullOrEmpty(text))
        {
            TextLayoutCacheKey layoutKey = new(text, currentStyle, new(finalBounds.Width, finalBounds.Height), textAlignment);

            if (!textLayoutCache.TryGetValue(layoutKey, out var textLayout))
            {
                textLayout = dwriteFactory.CreateTextLayout(text, textFormat, finalBounds.Width, finalBounds.Height);

                textLayout.TextAlignment = textAlignment.Horizontal switch
                {
                    HAlignment.Left => TextAlignment.Leading,
                    HAlignment.Center => TextAlignment.Center,
                    HAlignment.Right => TextAlignment.Trailing,
                    _ => TextAlignment.Leading
                };
                textLayout.ParagraphAlignment = textAlignment.Vertical switch
                {
                    VAlignment.Top => ParagraphAlignment.Near,
                    VAlignment.Center => ParagraphAlignment.Center,
                    VAlignment.Bottom => ParagraphAlignment.Far,
                    _ => ParagraphAlignment.Near
                };

                textLayoutCache[layoutKey] = textLayout;
            }

            Vector2 drawOrigin = new(finalBounds.X + textOffset.X, finalBounds.Y + textOffset.Y);
            renderTarget.DrawTextLayout(drawOrigin, textLayout, textBrush);
        }

        return wasClickedThisFrame;
    }
}