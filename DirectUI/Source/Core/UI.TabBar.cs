using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    private static bool TabButtonPrimitive(string id, string text, Vector2 size, bool isActive, TabStylePack theme, bool disabled)
    {
        if (!IsContextValid()) return false;

        var intId = id.GetHashCode();
        var position = Context.GetCurrentLayoutPosition();
        Rect bounds = new(position.X, position.Y, size.X, size.Y);

        InputState input = Context.InputState;
        bool isHovering = !disabled && bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        bool wasClicked = false;

        if (isHovering) State.SetPotentialInputTarget(intId);

        if (input.WasLeftMousePressedThisFrame && isHovering && State.PotentialInputTargetId == intId)
        {
            wasClicked = true;
        }

        theme.UpdateCurrentStyle(isHovering, isActive, disabled, false);

        var rt = Context.RenderTarget;
        Resources.DrawBoxStyleHelper(rt, new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Width, bounds.Height), theme.Current);

        var textBrush = Resources.GetOrCreateBrush(rt, theme.Current.FontColor);
        var textFormat = Resources.GetOrCreateTextFormat(Context.DWriteFactory, theme.Current);
        if (textBrush is not null && textFormat is not null && !string.IsNullOrEmpty(text))
        {
            textFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;
            textFormat.ParagraphAlignment = ParagraphAlignment.Center;
            rt.DrawText(text, textFormat, bounds, textBrush);
        }

        Context.AdvanceLayout(size);
        return wasClicked;
    }

    public static void TabBar(string id, string[] tabLabels, ref int activeIndex, TabStylePack? theme = null)
    {
        if (!IsContextValid() || tabLabels is null || tabLabels.Length == 0) return;

        var tabTheme = theme ?? State.GetOrCreateElement<TabStylePack>(id + "_theme_default");
        var textMargin = new Vector2(15, 5);
        float tabHeight = 30f;
        float maxWidth = 0;

        var styleForMeasuring = tabTheme.Normal;
        foreach (var label in tabLabels)
        {
            Vector2 measuredSize = Resources.MeasureText(Context.DWriteFactory, label, styleForMeasuring);
            if (measuredSize.X > maxWidth)
            {
                maxWidth = measuredSize.X;
            }
        }
        float uniformTabWidth = maxWidth + textMargin.X * 2;
        var tabSize = new Vector2(uniformTabWidth, tabHeight);

        BeginHBoxContainer(id + "_hbox", Context.GetCurrentLayoutPosition(), 0);
        for (int i = 0; i < tabLabels.Length; i++)
        {
            bool wasClicked = TabButtonPrimitive(
                id + "_" + i,
                tabLabels[i],
                tabSize,
                i == activeIndex,
                tabTheme,
                false
            );
            if (wasClicked)
            {
                activeIndex = i;
            }
        }
        EndHBoxContainer();
    }
}