using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    internal static bool StatelessButton(int id, Rect bounds, string text, ButtonStylePack stylePack, Alignment textAlignment, DirectUI.Button.ActionMode clickActionMode, bool autoWidth = false, Vector2? textMargin = null, Vector2 textOffset = default)
    {
        InputState input = Context.InputState;
        var renderTarget = Context.RenderTarget;
        var dwriteFactory = Context.DWriteFactory;

        Vector2 finalSize = new(bounds.Width, bounds.Height);
        if (autoWidth)
        {
            Vector2 measuredSize = Resources.MeasureText(dwriteFactory, text, stylePack.Normal);
            Vector2 margin = textMargin ?? new Vector2(10, 5);
            finalSize.X = measuredSize.X + margin.X * 2;
        }
        Rect finalBounds = new(bounds.X, bounds.Y, finalSize.X, finalSize.Y);

        bool isHovering = finalBounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        bool isPressed = State.ActivelyPressedElementId == id;
        bool isFocused = State.FocusedElementId == id;
        bool wasClickedThisFrame = false;

        if (isHovering) State.SetPotentialInputTarget(id);

        if (!input.IsLeftMouseDown && isPressed)
        {
            if (isHovering && clickActionMode == DirectUI.Button.ActionMode.Release) wasClickedThisFrame = true;
            State.ClearActivePress(id);
            isPressed = false;
        }

        if (input.WasLeftMousePressedThisFrame && isHovering && State.PotentialInputTargetId == id && !State.DragInProgressFromPreviousFrame)
        {
            State.SetButtonPotentialCaptorForFrame(id);
            State.SetFocus(id);
            isPressed = true;
        }

        if (!wasClickedThisFrame && clickActionMode == DirectUI.Button.ActionMode.Press && State.InputCaptorId == id) wasClickedThisFrame = true;

        stylePack.UpdateCurrentStyle(isHovering, isPressed, false, isFocused);
        ButtonStyle currentStyle = stylePack.Current;
        Resources.DrawBoxStyleHelper(renderTarget, new(finalBounds.X, finalBounds.Y), new(finalBounds.Width, finalBounds.Height), currentStyle);

        var textBrush = Resources.GetOrCreateBrush(renderTarget, currentStyle.FontColor);
        var textFormat = Resources.GetOrCreateTextFormat(dwriteFactory, currentStyle);
        if (textBrush is not null && textFormat is not null && !string.IsNullOrEmpty(text))
        {
            var layoutKey = new UIResources.TextLayoutCacheKey(text, currentStyle, new(finalBounds.Width, finalBounds.Height), textAlignment);
            if (!Resources.textLayoutCache.TryGetValue(layoutKey, out var textLayout))
            {
                textLayout = dwriteFactory.CreateTextLayout(text, textFormat, finalBounds.Width, finalBounds.Height);
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
                Resources.textLayoutCache[layoutKey] = textLayout;
            }
            renderTarget.DrawTextLayout(new Vector2(finalBounds.X + textOffset.X, finalBounds.Y + textOffset.Y), textLayout, textBrush, DrawTextOptions.None);
        }
        return wasClickedThisFrame;
    }
}