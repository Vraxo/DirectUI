using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    private static bool TabButtonPrimitive(int id, string text, Vector2 size, bool isActive, TabStylePack theme, bool disabled)
    {
        if (!IsContextValid()) return false;

        var intId = id;
        var position = Context.Layout.GetCurrentPosition();
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
        var currentStyle = theme.Current;

        var rt = Context.RenderTarget;
        Resources.DrawBoxStyleHelper(rt, new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Width, bounds.Height), currentStyle);

        var textBrush = Resources.GetOrCreateBrush(rt, currentStyle.FontColor);
        if (textBrush is not null && !string.IsNullOrEmpty(text))
        {
            var dwriteFactory = Context.DWriteFactory;
            var textAlignment = new Alignment(HAlignment.Center, VAlignment.Center);

            var layoutKey = new UIResources.TextLayoutCacheKey(text, currentStyle, size, textAlignment);
            if (!Resources.textLayoutCache.TryGetValue(layoutKey, out var textLayout))
            {
                var textFormat = Resources.GetOrCreateTextFormat(dwriteFactory, currentStyle);
                if (textFormat is not null)
                {
                    textLayout = dwriteFactory.CreateTextLayout(text, textFormat, size.X, size.Y);
                    textLayout.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;
                    textLayout.ParagraphAlignment = ParagraphAlignment.Center;
                    Resources.textLayoutCache[layoutKey] = textLayout;
                }
            }

            if (textLayout is not null)
            {
                rt.DrawTextLayout(new Vector2(bounds.X, bounds.Y), textLayout, textBrush);
            }
        }

        Context.Layout.AdvanceLayout(size);
        return wasClicked;
    }

    public static void TabBar(int id, string[] tabLabels, ref int activeIndex, TabStylePack? theme = null)
    {
        if (!IsContextValid() || tabLabels is null || tabLabels.Length == 0) return;

        var themeId = HashCode.Combine(id, "theme_default");
        var tabTheme = theme ?? State.GetOrCreateElement<TabStylePack>(themeId);
        var state = State.GetOrCreateElement<TabBarState>(id);

        const float textMarginX = 15f;
        const float tabHeight = 30f;
        float uniformTabWidth;

        if (state.CachedUniformWidth < 0) // Not calculated yet, or invalidated.
        {
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
            uniformTabWidth = maxWidth + textMarginX * 2;
            state.CachedUniformWidth = uniformTabWidth;
        }
        else
        {
            uniformTabWidth = state.CachedUniformWidth;
        }

        var tabSize = new Vector2(uniformTabWidth, tabHeight);

        var hboxId = HashCode.Combine(id, "hbox");
        BeginHBoxContainer(hboxId, Context.Layout.GetCurrentPosition(), 0);
        for (int i = 0; i < tabLabels.Length; i++)
        {
            var buttonId = HashCode.Combine(id, i);
            bool wasClicked = TabButtonPrimitive(
                buttonId,
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