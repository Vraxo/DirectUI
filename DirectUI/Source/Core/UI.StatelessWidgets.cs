using DirectUI;
using System.Buffers.Text;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    /// <summary>
    /// An internal, high-performance, stateless button drawing function.
    /// It does not use the GetOrCreateElement cache and avoids allocations.
    /// This is ideal for performance-critical loops like tree views.
    /// </summary>
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
        var input = CurrentInputState;
        var renderTarget = CurrentRenderTarget!;
        var dwriteFactory = CurrentDWriteFactory!;

        Vector2 finalSize = new(bounds.Width, bounds.Height);
        if (autoWidth)
        {
            var measuredSize = MeasureText(dwriteFactory, text, stylePack.Normal);
            var margin = textMargin ?? new Vector2(10, 5);
            finalSize.X = measuredSize.X + margin.X * 2;
        }
        Rect finalBounds = new Rect(bounds.X, bounds.Y, finalSize.X, finalSize.Y);

        // --- Input and State ---
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
            isPressed = false; // Update visual state immediately
        }

        if (input.WasLeftMousePressedThisFrame && isHovering && PotentialInputTargetId == id && !dragInProgressFromPreviousFrame)
        {
            SetButtonPotentialCaptorForFrame(id);
            isPressed = true; // Update visual state immediately
        }

        if (!wasClickedThisFrame && clickActionMode == DirectUI.Button.ActionMode.Press && InputCaptorId == id)
        {
            wasClickedThisFrame = true;
        }

        // --- Drawing ---
        stylePack.UpdateCurrentStyle(isHovering, isPressed, false);
        var currentStyle = stylePack.Current;

        DrawBoxStyleHelper(renderTarget, new Vector2(finalBounds.X, finalBounds.Y), new Vector2(finalBounds.Width, finalBounds.Height), currentStyle);

        // Draw Text
        ID2D1SolidColorBrush textBrush = GetOrCreateBrush(currentStyle.FontColor);
        IDWriteTextFormat? textFormat = GetOrCreateTextFormat(currentStyle);
        if (textBrush is not null && textFormat is not null && !string.IsNullOrEmpty(text))
        {
            // --- OPTIMIZATION: Use Text Layout Cache ---
            var layoutKey = new TextLayoutCacheKey(text, currentStyle, new Vector2(finalBounds.Width, finalBounds.Height), textAlignment);

            if (!textLayoutCache.TryGetValue(layoutKey, out var textLayout))
            {
                // Not in cache: Create, configure, and cache the layout.
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

                // Add to cache. This object will now be disposed by UI.CleanupResources.
                textLayoutCache[layoutKey] = textLayout;
            }

            // Do not dispose the layout here as it is cached.
            var drawOrigin = new Vector2(finalBounds.X + textOffset.X, finalBounds.Y + textOffset.Y);
            renderTarget.DrawTextLayout(drawOrigin, textLayout, textBrush);
        }

        return wasClickedThisFrame;
    }
}