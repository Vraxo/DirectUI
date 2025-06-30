using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    public static bool Checkbox(string id, string label, ref bool isChecked, bool disabled = false)
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

        var labelSize = string.IsNullOrEmpty(label) ? Vector2.Zero : Resources.MeasureText(Context.DWriteFactory, label, textStyle);
        var totalSize = new Vector2(boxSize.X + (labelSize.X > 0 ? spacing + labelSize.X : 0), Math.Max(boxSize.Y, labelSize.Y));
        var drawPos = Context.Layout.GetCurrentPosition();

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
        var rt = Context.RenderTarget;
        float boxY = drawPos.Y + (totalSize.Y - boxSize.Y) / 2; // Vertically center the box
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

        Resources.DrawBoxStyleHelper(rt, boxRect.TopLeft, new Vector2(boxRect.Width, boxRect.Height), boxStyle);

        // Draw the checkmark if checked
        if (isChecked)
        {
            // The checkmarkColor variable was changed above to pure white.
            var checkBrush = Resources.GetOrCreateBrush(rt, checkmarkColor);
            if (checkBrush != null)
            {
                // A simple checkmark drawn as two lines
                float pad = boxSize.X * 0.25f;
                var p1 = new Vector2(boxRect.Left + pad, boxRect.Top + boxSize.Y * 0.5f);
                var p2 = new Vector2(boxRect.Left + boxSize.X * 0.45f, boxRect.Bottom - pad);
                var p3 = new Vector2(boxRect.Right - pad, boxRect.Top + pad);
                rt.DrawLine(p1, p2, checkBrush, 2.0f);
                rt.DrawLine(p2, p3, checkBrush, 2.0f);
            }
        }

        // Draw the label
        if (!string.IsNullOrEmpty(label))
        {
            var labelPos = new Vector2(boxRect.Right + spacing, drawPos.Y);
            var labelBounds = new Rect(labelPos.X, labelPos.Y, labelSize.X, totalSize.Y);
            DrawLabelText(rt, Context.DWriteFactory, Resources, labelBounds, label, textStyle, new Alignment(HAlignment.Left, VAlignment.Center));
        }

        Context.Layout.AdvanceLayout(totalSize);
        return clicked;
    }
}