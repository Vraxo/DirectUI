using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    public static void TabBar(string id, string[] tabLabels, ref int activeIndex, ButtonStylePack? theme = null)
    {
        if (!IsContextValid() || tabLabels is null || tabLabels.Length == 0) return;

        int intId = id.GetHashCode();
        var themeId = HashCode.Combine(intId, "theme_default");
        var tabTheme = theme ?? State.GetOrCreateElement<ButtonStylePack>(themeId);
        var state = State.GetOrCreateElement<TabBarState>(intId);

        const float textMarginX = 15f;
        const float tabHeight = 30f;
        float uniformTabWidth;

        if (state.CachedUniformWidth < 0) // Not calculated yet, or invalidated.
        {
            float maxWidth = 0;
            var styleForMeasuring = tabTheme.Normal;
            foreach (var label in tabLabels)
            {
                // Use ITextService to measure text
                Vector2 measuredSize = Context.TextService.MeasureText(label, styleForMeasuring);
                if (measuredSize.X > maxWidth)
                {
                    maxWidth = measuredSize.X;
                }
            }
            uniformTabWidth = maxWidth + textMarginX * 2;
            state.CachedUniformWidth = uniformTabWidth;
        }
        else
        {
            uniformTabWidth = state.CachedUniformWidth;
        }

        var tabSize = new Vector2(uniformTabWidth, tabHeight);

        var hboxIdString = id + "_hbox";
        BeginHBoxContainer(hboxIdString, Context.Layout.GetCurrentPosition(), 0);
        for (int i = 0; i < tabLabels.Length; i++)
        {
            var buttonId = HashCode.Combine(intId, i);
            var position = Context.Layout.GetCurrentPosition();
            var bounds = new Rect(position.X, position.Y, tabSize.X, tabSize.Y);

            bool wasClicked = DrawButtonPrimitive(
                buttonId,
                bounds,
                tabLabels[i],
                tabTheme,
                disabled: false,
                textAlignment: new Alignment(HAlignment.Center, VAlignment.Center),
                clickMode: DirectUI.Button.ActionMode.Release,
                clickBehavior: DirectUI.Button.ClickBehavior.Left,
                textOffset: Vector2.Zero,
                isActive: i == activeIndex
            );

            if (wasClicked)
            {
                activeIndex = i;
            }
            Context.Layout.AdvanceLayout(tabSize);
        }
        EndHBoxContainer();
    }
}