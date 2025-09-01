// DirectUI/Source/Core/UI.DataGrid.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using DirectUI.Drawing;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo?>> _propertyInfoCache = new();

    /// <summary>
    /// Renders a grid to display tabular data.
    /// </summary>
    /// <typeparam name="T">The type of data items in the collection.</typeparam>
    /// <param name="id">A unique identifier for the data grid.</param>
    /// <param name="items">The collection of data items to display.</param>
    /// <param name="columns">The column definitions for the grid.</param>
    /// <param name="selectedIndex">A reference to the index of the currently selected item.</param>
    /// <param name="size">The total size of the data grid control.</param>
    /// <param name="position">An optional absolute position for the grid. If not provided, it uses the current layout position.</param>
    public static void DataGrid<T>(
        string id,
        IReadOnlyList<T> items,
        IReadOnlyList<DataGridColumn> columns,
        ref int selectedIndex,
        Vector2 size,
        Vector2 position = default)
    {
        if (!IsContextValid()) return;

        int intId = id.GetHashCode();
        var state = State.GetOrCreateElement<DataGridState>(intId);
        Vector2 drawPos = Context.Layout.ApplyLayout(position);

        // Culling
        var gridBounds = new Rect(drawPos.X, drawPos.Y, size.X, size.Y);
        if (!Context.Layout.IsRectVisible(gridBounds))
        {
            Context.Layout.AdvanceLayout(size);
            return;
        }

        InitializeDataGridState(state, intId, columns);

        // --- Style Definitions ---
        float headerHeight = 28f;
        float rowHeight = 24f;
        const float scrollbarThickness = 12f;

        var gridStyle = new BoxStyle { FillColor = new(0.1f, 0.1f, 0.1f, 1.0f), BorderLength = 0f, Roundness = 0f };
        var headerStyle = new ButtonStyle { FillColor = new(0.2f, 0.2f, 0.2f, 1.0f), Roundness = 0, BorderLength = 0, BorderColor = Colors.Transparent, FontColor = DefaultTheme.Text };
        var rowStyle = new ButtonStylePack { Roundness = 0f, BorderLength = 0f };
        rowStyle.Normal.FillColor = DefaultTheme.Transparent;
        rowStyle.Hover.FillColor = new Color4(0.25f, 0.25f, 0.25f, 1.0f);
        rowStyle.Active.FillColor = DefaultTheme.Accent;
        rowStyle.ActiveHover.FillColor = DefaultTheme.Accent;

        // --- Layout Calculation ---
        var headerBounds = new Rect(drawPos.X, drawPos.Y, size.X, headerHeight);
        var contentBounds = new Rect(drawPos.X, drawPos.Y + headerHeight, size.X, size.Y - headerHeight);
        float totalContentWidth = state.ColumnWidths.Sum();
        float totalContentHeight = items.Count * rowHeight;

        // --- Draw Main Background ---
        Context.Renderer.DrawBox(gridBounds, gridStyle);

        // --- Draw Header ---
        DrawDataGridHeader(intId, state, columns, headerBounds, headerStyle);

        // --- Handle Scrolling and Draw Rows ---
        bool hScrollVisible = totalContentWidth > contentBounds.Width;
        bool vScrollVisible = totalContentHeight > contentBounds.Height;

        float viewWidth = contentBounds.Width - (vScrollVisible ? scrollbarThickness : 0);
        float viewHeight = contentBounds.Height - (hScrollVisible ? scrollbarThickness : 0);

        // Use a local variable for the struct to avoid CS1612
        var scrollOffset = state.ScrollOffset;

        // Handle Mouse Wheel Scrolling
        bool isHoveringContent = contentBounds.Contains(Context.InputState.MousePosition);
        if (isHoveringContent && Context.InputState.ScrollDelta != 0)
        {
            // Scroll delta is inverted: up is positive, but content moves down (offset increases)
            // A standard mouse wheel tick is 120, and the delta is usually +/- 1.0f in the input system.
            // Scrolling by 3 rows feels natural.
            scrollOffset.Y -= Context.InputState.ScrollDelta * rowHeight * 3;
        }

        // Clamp scroll offsets
        scrollOffset.X = Math.Clamp(scrollOffset.X, 0, Math.Max(0, totalContentWidth - viewWidth));
        scrollOffset.Y = Math.Clamp(scrollOffset.Y, 0, Math.Max(0, totalContentHeight - viewHeight));
        state.ScrollOffset = scrollOffset; // Assign back before drawing rows

        DrawDataGridRows(intId, items, columns, state, contentBounds, new Vector2(viewWidth, viewHeight), rowHeight, ref selectedIndex, rowStyle);

        // --- Draw Scrollbars ---
        // Re-read from state in case it was modified, then modify and write back.
        scrollOffset = state.ScrollOffset;
        if (vScrollVisible)
        {
            scrollOffset.Y = VScrollBar(
                id + "_vscroll",
                scrollOffset.Y,
                new Vector2(contentBounds.Right - scrollbarThickness, contentBounds.Y),
                contentBounds.Height - (hScrollVisible ? scrollbarThickness : 0), // Adjust track height if HScroll is visible
                totalContentHeight,
                viewHeight,
                scrollbarThickness
            );
        }
        if (hScrollVisible)
        {
            scrollOffset.X = HScrollBar(
                id + "_hscroll",
                scrollOffset.X,
                new Vector2(contentBounds.X, contentBounds.Bottom - scrollbarThickness),
                contentBounds.Width - (vScrollVisible ? scrollbarThickness : 0),
                totalContentWidth,
                viewWidth,
                scrollbarThickness
            );
        }
        state.ScrollOffset = scrollOffset; // Assign the final value back

        Context.Layout.AdvanceLayout(size);
    }

    private static void InitializeDataGridState(DataGridState state, int id, IReadOnlyList<DataGridColumn> columns)
    {
        if (state.Id == id && state.ColumnWidths.Count == columns.Count) return;
        state.Id = id;
        state.ColumnWidths.Clear();
        state.ColumnWidths.AddRange(columns.Select(c => c.InitialWidth));
    }

    private static void DrawDataGridHeader(int id, DataGridState state, IReadOnlyList<DataGridColumn> columns, Rect headerBounds, ButtonStyle style)
    {
        var input = Context.InputState;
        float currentX = headerBounds.X - state.ScrollOffset.X;

        Context.Renderer.PushClipRect(headerBounds);

        for (int i = 0; i < columns.Count; i++)
        {
            float colWidth = state.ColumnWidths[i];
            var colHeaderBounds = new Rect(currentX, headerBounds.Y, colWidth, headerBounds.Height);
            Context.Renderer.DrawBox(colHeaderBounds, style);
            DrawTextPrimitive(colHeaderBounds, columns[i].HeaderText, style, new Alignment(HAlignment.Left, VAlignment.Center), new Vector2(5, 0));

            // Column Resizer Handle
            var handleId = HashCode.Combine(id, "resize", i);
            var handleBounds = new Rect(currentX + colWidth - 2, headerBounds.Y, 4, headerBounds.Height);

            bool isHoveringHandle = handleBounds.Contains(input.MousePosition);
            if (isHoveringHandle) State.SetPotentialInputTarget(handleId);

            if (State.ActivelyPressedElementId == handleId && input.IsLeftMouseDown)
            {
                float deltaX = input.MousePosition.X - state.DragStartMouseX;
                state.ColumnWidths[i] = Math.Max(20, state.ColumnResizeStartWidth + deltaX);
            }
            else if (State.ActivelyPressedElementId == handleId && !input.IsLeftMouseDown)
            {
                State.ClearActivePress(handleId);
            }

            if (isHoveringHandle && input.WasLeftMousePressedThisFrame && State.PotentialInputTargetId == handleId)
            {
                State.SetPotentialCaptorForFrame(handleId);
                state.ResizingColumnIndex = i;
                state.DragStartMouseX = input.MousePosition.X;
                state.ColumnResizeStartWidth = state.ColumnWidths[i];
            }

            currentX += colWidth;
        }

        Context.Renderer.PopClipRect();
    }

    private static void DrawDataGridRows<T>(int id, IReadOnlyList<T> items, IReadOnlyList<DataGridColumn> columns, DataGridState state, Rect contentBounds, Vector2 viewSize, float rowHeight, ref int selectedIndex, ButtonStylePack rowStyle)
    {
        var input = Context.InputState;

        Context.Renderer.PushClipRect(new Rect(contentBounds.X, contentBounds.Y, viewSize.X, viewSize.Y));

        int firstVisibleRow = (int)Math.Floor(state.ScrollOffset.Y / rowHeight);
        int visibleRowCount = (int)Math.Ceiling(viewSize.Y / rowHeight) + 1;
        int lastVisibleRow = Math.Min(items.Count - 1, firstVisibleRow + visibleRowCount);

        for (int i = firstVisibleRow; i <= lastVisibleRow; i++)
        {
            var item = items[i];
            float rowY = contentBounds.Y + (i * rowHeight) - state.ScrollOffset.Y;
            var rowBounds = new Rect(contentBounds.X, rowY, contentBounds.Width, rowHeight); // Use full width for selection
            bool isSelected = i == selectedIndex;

            int rowId = HashCode.Combine(id, "row", i);
            if (DrawButtonPrimitive(rowId, rowBounds, "", rowStyle, false, default, DirectUI.Button.ActionMode.Press, DirectUI.Button.ClickBehavior.Left, Vector2.Zero, isSelected))
            {
                selectedIndex = i;
            }

            float currentX = contentBounds.X - state.ScrollOffset.X;
            for (int j = 0; j < columns.Count; j++)
            {
                var column = columns[j];
                float colWidth = state.ColumnWidths[j];
                var cellBounds = new Rect(currentX, rowY, colWidth, rowHeight);

                var value = GetPropertyValue(item, column.DataPropertyName);
                string cellText = value?.ToString() ?? string.Empty;

                if (value is TimeSpan ts)
                {
                    cellText = $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}";
                }

                Context.Renderer.PushClipRect(cellBounds); // Clip text to cell
                DrawTextPrimitive(cellBounds, cellText, rowStyle.Current, new Alignment(HAlignment.Left, VAlignment.Center), new Vector2(5, 0));
                Context.Renderer.PopClipRect();

                currentX += colWidth;
            }
        }

        Context.Renderer.PopClipRect();
    }

    private static object? GetPropertyValue<T>(T item, string propertyName)
    {
        if (item == null || string.IsNullOrEmpty(propertyName)) return null;
        var type = typeof(T);

        if (!_propertyInfoCache.TryGetValue(type, out var propertyMap))
        {
            propertyMap = new Dictionary<string, PropertyInfo?>();
            _propertyInfoCache[type] = propertyMap;
        }

        if (!propertyMap.TryGetValue(propertyName, out var propInfo))
        {
            propInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            propertyMap[propertyName] = propInfo;
        }

        return propInfo?.GetValue(item);
    }
}