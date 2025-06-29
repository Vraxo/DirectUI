// Core/UI.ScrollBar.cs
using System;
using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    /// <summary>
    /// Draws a vertical scrollbar and handles its interaction.
    /// </summary>
    /// <param name="id">A unique identifier for the scrollbar.</param>
    /// <param name="currentScrollOffset">The current vertical scroll offset, which this control will modify.</param>
    /// <param name="position">The top-left position where the scrollbar will be drawn.</param>
    /// <param name="trackHeight">The total height of the scrollbar track.</param>
    /// <param name="contentHeight">The total height of the content being scrolled.</param>
    /// <param name="visibleHeight">The height of the visible portion of the content.</param>
    /// <param name="thickness">The width of the scrollbar.</param>
    /// <param name="theme">The style for the scrollbar's track.</param>
    /// <param name="thumbTheme">The style for the scrollbar's draggable thumb.</param>
    /// <returns>The new vertical scroll offset after user interaction.</returns>
    public static float VScrollBar(
        string id,
        float currentScrollOffset,
        Vector2 position,
        float trackHeight,
        float contentHeight,
        float visibleHeight,
        float thickness = 12f,
        SliderStyle? theme = null,
        ButtonStylePack? thumbTheme = null)
    {
        if (!IsContextValid()) return currentScrollOffset;

        int intId = id.GetHashCode();
        InternalScrollBarLogic scrollBarInstance = State.GetOrCreateElement<InternalScrollBarLogic>(intId);

        // Configure instance
        scrollBarInstance.Position = position;
        scrollBarInstance.TrackLength = trackHeight;
        scrollBarInstance.TrackThickness = thickness;
        scrollBarInstance.IsVertical = true;
        scrollBarInstance.ContentSize = contentHeight;
        scrollBarInstance.VisibleSize = visibleHeight;
        scrollBarInstance.Theme = theme ?? scrollBarInstance.Theme ?? new SliderStyle();
        scrollBarInstance.ThumbTheme = thumbTheme ?? scrollBarInstance.ThumbTheme ?? new ButtonStylePack();

        float newScrollOffset = scrollBarInstance.UpdateAndDraw(intId, currentScrollOffset);

        // A scrollbar, being an overlay, does not advance the main layout cursor.

        return newScrollOffset;
    }

    /// <summary>
    /// Draws a horizontal scrollbar and handles its interaction.
    /// </summary>
    /// <param name="id">A unique identifier for the scrollbar.</param>
    /// <param name="currentScrollOffset">The current horizontal scroll offset, which this control will modify.</param>
    /// <param name="position">The top-left position where the scrollbar will be drawn.</param>
    /// <param name="trackWidth">The total width of the scrollbar track.</param>
    /// <param name="contentWidth">The total width of the content being scrolled.</param>
    /// <param name="visibleWidth">The width of the visible portion of the content.</param>
    /// <param name="thickness">The height of the scrollbar.</param>
    /// <param name="theme">The style for the scrollbar's track.</param>
    /// <param name="thumbTheme">The style for the scrollbar's draggable thumb.</param>
    /// <returns>The new horizontal scroll offset after user interaction.</returns>
    public static float HScrollBar(
        string id,
        float currentScrollOffset,
        Vector2 position,
        float trackWidth,
        float contentWidth,
        float visibleWidth,
        float thickness = 12f,
        SliderStyle? theme = null,
        ButtonStylePack? thumbTheme = null)
    {
        if (!IsContextValid()) return currentScrollOffset;

        int intId = id.GetHashCode();
        InternalScrollBarLogic scrollBarInstance = State.GetOrCreateElement<InternalScrollBarLogic>(intId);

        // Configure instance
        scrollBarInstance.Position = position;
        scrollBarInstance.TrackLength = trackWidth;
        scrollBarInstance.TrackThickness = thickness;
        scrollBarInstance.IsVertical = false;
        scrollBarInstance.ContentSize = contentWidth;
        scrollBarInstance.VisibleSize = visibleWidth;
        scrollBarInstance.Theme = theme ?? scrollBarInstance.Theme ?? new SliderStyle();
        scrollBarInstance.ThumbTheme = thumbTheme ?? scrollBarInstance.ThumbTheme ?? new ButtonStylePack();

        float newScrollOffset = scrollBarInstance.UpdateAndDraw(intId, currentScrollOffset);

        return newScrollOffset;
    }
}