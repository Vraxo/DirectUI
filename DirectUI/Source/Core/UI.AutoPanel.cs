using System;
using System.Numerics;
using DirectUI.Drawing;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    /// <summary>
    /// A panel that automatically calculates its height to fit its content, based on a fixed logical width.
    /// This widget encapsulates a two-pass layout process: first measuring the content, then drawing.
    /// </summary>
    /// <param name="id">A unique identifier for the panel.</param>
    /// <param name="logicalWidth">The fixed logical width of the panel.</param>
    /// <param name="drawContent">An action that draws the panel's content. It receives the available inner logical width.</param>
    /// <param name="style">The style for the panel's background.</param>
    /// <param name="padding">The logical padding between the panel's border and its content.</param>
    /// <param name="gap">The logical vertical gap between elements drawn inside the panel.</param>
    /// <returns>The final physical bounds of the drawn panel.</returns>
    public static Rect AutoPanel(
        string id,
        float logicalWidth,
        Action<float> drawContent,
        BoxStyle? style = null,
        Vector2 padding = default,
        float gap = 5f)
    {
        if (!IsContextValid()) return default;

        int intId = id.GetHashCode();
        var context = Context;
        var scale = context.UIScale;

        Vector2 finalPadding = (padding == default) ? new Vector2(5, 5) : padding;
        float innerContentLogicalWidth = logicalWidth - finalPadding.X * 2;
        if (innerContentLogicalWidth < 0) innerContentLogicalWidth = 0;

        // --- 1. Calculation Pass ---
        var contentLogicalSize = CalculateLayout(() =>
        {
            BeginVBoxContainer(id + "_calc", Vector2.Zero, gap);
            drawContent(innerContentLogicalWidth);
            EndVBoxContainer();
        });

        // Use the calculated height. The width is derived from the input parameter.
        float contentLogicalHeight = contentLogicalSize.Y;
        var panelLogicalSize = new Vector2(logicalWidth, contentLogicalHeight + finalPadding.Y * 2);

        // --- 2. Geometry Calculation ---
        var panelLogicalPosition = context.Layout.GetCurrentPosition();
        var panelPhysicalPosition = panelLogicalPosition * scale;
        var panelPhysicalSize = panelLogicalSize * scale;
        var panelBounds = new Rect(panelPhysicalPosition.X, panelPhysicalPosition.Y, panelPhysicalSize.X, panelPhysicalSize.Y);

        // --- Culling ---
        if (!context.Layout.IsRectVisible(panelBounds))
        {
            context.Layout.AdvanceLayout(panelLogicalSize);
            return panelBounds;
        }

        // --- 3. Drawing Pass ---
        var finalStyle = style ?? new BoxStyle { FillColor = new Color(30, 30, 30, 255), Roundness = 0.1f, BorderLength = 0f };
        context.Renderer.DrawBox(panelBounds, finalStyle);

        var contentLogicalPosition = panelLogicalPosition + finalPadding;
        var contentPhysicalPosition = contentLogicalPosition * scale;
        var contentPhysicalSize = new Vector2(innerContentLogicalWidth, contentLogicalHeight) * scale;
        var contentClipRect = new Rect(contentPhysicalPosition.X, contentPhysicalPosition.Y, contentPhysicalSize.X, contentPhysicalSize.Y);

        bool pushedClip = false;
        if (contentClipRect.Width > 0 && contentClipRect.Height > 0)
        {
            context.Renderer.PushClipRect(contentClipRect);
            context.Layout.PushClipRect(contentClipRect);
            pushedClip = true;
        }

        BeginVBoxContainer(id + "_draw", contentLogicalPosition, gap);
        drawContent(innerContentLogicalWidth);
        EndVBoxContainer(advanceParentLayout: false); // The VBox doesn't advance its parent. We do it manually below.

        if (pushedClip)
        {
            context.Layout.PopClipRect();
            context.Renderer.PopClipRect();
        }

        // --- 4. Advance Parent Layout ---
        context.Layout.AdvanceLayout(panelLogicalSize);

        return panelBounds;
    }
}