using System.Numerics;
using Vortice.Mathematics;
using Vortice.Direct2D1; // Still needed for AntialiasMode enum

namespace DirectUI;

public static partial class UI
{
    public static bool Checkbox(string id, string label, ref bool isChecked, bool disabled = false, Vector2? size = null)
    {
        if (!IsContextValid()) return false;

        int intId = id.GetHashCode();

        // --- Style and Sizing (UE5 Theme Adjustments) ---
        var boxSize = new Vector2(16, 16);
        var spacing = 5f;
        // Checkmark is now pure white.
        var checkmarkColor = Colors.White;
        // Background is a specific dark grey.
        var normalFillColor = new Color4(43 / 255f, 45 / 255f, 47 / 255f, 1.0f); // #2B2D2F
        var textColor = disabled ? DefaultTheme.DisabledText : DefaultTheme.Text;
        var textStyle = new ButtonStyle { FontColor = textColor }; // Use ButtonStyle for font properties.

        var labelSize = string.IsNullOrEmpty(label) ? Vector2.Zero : Context.TextService.MeasureText(label, textStyle);

        var contentWidth = boxSize.X + (labelSize.X > 0 ? spacing + labelSize.X : 0);
        var contentHeight = Math.Max(boxSize.Y, labelSize.Y);

        var finalWidgetHeight = size?.Y > 0 ? size.Value.Y : contentHeight;
        var finalWidgetWidth = size?.X > 0 ? size.Value.X : contentWidth;
        var totalSize = new Vector2(finalWidgetWidth, finalWidgetHeight);

        var drawPos = Context.Layout.GetCurrentPosition();

        // New: Automatically adjust vertical position for HBox alignment
        if (Context.Layout.IsInLayoutContainer() && Context.Layout.PeekContainer() is HBoxContainerState hbox)
        {
            if (hbox.VerticalAlignment != VAlignment.Top && hbox.FixedRowHeight.HasValue)
            {
                float yOffset = 0;
                switch (hbox.VerticalAlignment)
                {
                    case VAlignment.Center:
                        yOffset = (hbox.FixedRowHeight.Value - totalSize.Y) / 2f;
                        break;
                    case VAlignment.Bottom:
                        yOffset = hbox.FixedRowHeight.Value - totalSize.Y;
                        break;
                }
                drawPos.Y += yOffset;
            }
        }

        var widgetBounds = new Rect(drawPos.X, drawPos.Y, totalSize.X, totalSize.Y);

        // --- Culling ---
        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(totalSize);
            return false;
        }

        // --- Interaction ---
        bool clicked = false;
        var input = Context.InputState;
        bool isHovering = !disabled && widgetBounds.Contains(input.MousePosition);

        if (isHovering)
        {
            State.SetPotentialInputTarget(intId);
        }

        if (!disabled && isHovering && input.WasLeftMousePressedThisFrame && State.PotentialInputTargetId == intId)
        {
            clicked = true;
            isChecked = !isChecked;
            State.SetFocus(intId);
        }

        // --- Drawing ---
        var renderer = Context.Renderer;
        // This calculation centers the box inside the widget's total height.
        // A small vertical adjustment is added to compensate for font metrics in DrawTextPrimitive,
        // ensuring the checkbox and text align perfectly.
        const float yOffsetCorrection = -1.5f;
        float boxY = drawPos.Y + (totalSize.Y - boxSize.Y) / 2 + yOffsetCorrection;
        var boxRect = new Rect(drawPos.X, boxY, boxSize.X, boxSize.Y);

        // Draw the box frame
        var boxStyle = new BoxStyle();
        if (disabled)
        {
            boxStyle.FillColor = DefaultTheme.DisabledFill;
            boxStyle.BorderColor = DefaultTheme.DisabledBorder;
        }
        else if (isHovering)
        {
            boxStyle.FillColor = DefaultTheme.HoverFill;
            boxStyle.BorderColor = DefaultTheme.HoverBorder;
        }
        else
        {
            // Use the specified dark grey for the normal background.
            boxStyle.FillColor = normalFillColor;
            boxStyle.BorderColor = DefaultTheme.NormalBorder;
        }

        if (State.FocusedElementId == intId)
        {
            boxStyle.BorderColor = DefaultTheme.FocusBorder;
        }
        boxStyle.Roundness = 0.2f;
        boxStyle.BorderLength = 1f;

        renderer.DrawBox(boxRect, boxStyle);

        // Draw the checkmark if checked
        if (isChecked)
        {
            // The checkmarkColor variable was changed above to pure white.
            // A simple checkmark drawn as two lines
            float pad = boxSize.X * 0.25f;
            var p1 = new Vector2(boxRect.Left + pad, boxRect.Top + boxSize.Y * 0.5f);
            var p2 = new Vector2(boxRect.Left + boxSize.X * 0.45f, boxRect.Bottom - pad);
            var p3 = new Vector2(boxRect.Right - pad, boxRect.Top + pad);
            renderer.DrawLine(p1, p2, checkmarkColor, 2.0f);
            renderer.DrawLine(p2, p3, checkmarkColor, 2.0f);
        }

        // Draw the label
        if (!string.IsNullOrEmpty(label))
        {
            var labelPos = new Vector2(boxRect.Right + spacing, drawPos.Y);
            var labelBounds = new Rect(labelPos.X, labelPos.Y, labelSize.X, totalSize.Y);
            DrawTextPrimitive(labelBounds, label, textStyle, new Alignment(HAlignment.Left, VAlignment.Center), Vector2.Zero);
        }

        Context.Layout.AdvanceLayout(totalSize);
        return clicked;
    }
}