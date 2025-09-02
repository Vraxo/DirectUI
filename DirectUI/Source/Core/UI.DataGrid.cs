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
    /// <param name="selectedIndex">A reference to the index of the currently selected item in the original 'items' list.</param>
    /// <param name="size">The total size of the data grid control.</param>
    /// <param name="position">An optional absolute position for the grid. If not provided, it uses the current layout position.</param>
    /// <param name="autoSizeColumns">If true, columns are proportionally resized to fit the available width, disabling horizontal scrolling and user resizing.</param>
    /// <param name="trimCellText">If true, text that overflows a cell's width will be truncated and appended with an ellipsis (...).</param>
    public static void DataGrid<T>(
        string id,
        IReadOnlyList<T> items,
        IReadOnlyList<DataGridColumn> columns,
        ref int selectedIndex,
        Vector2 size,
        Vector2 position = default,
        bool autoSizeColumns = false,
        bool trimCellText = false)
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

        // Store the currently selected item before we sort, so we can find it again.
        T? currentSelectedItem = default;
        if (selectedIndex >= 0 && selectedIndex < items.Count)
        {
            currentSelectedItem = items[selectedIndex];
        }

        // Create a mutable, sorted copy of the items for display.
        List<T> sortedItems = new List<T>(items);
        if (state.SortColumnIndex >= 0 && state.SortColumnIndex < columns.Count)
        {
            var sortColumn = columns[state.SortColumnIndex];
            try
            {
                sortedItems.Sort((a, b) =>
                {
                    var valA = GetPropertyValue(a, sortColumn.DataPropertyName);
                    var valB = GetPropertyValue(b, sortColumn.DataPropertyName);

                    int compareResult;
                    if (valA is null && valB is null) compareResult = 0;
                    else if (valA is null) compareResult = -1;
                    else if (valB is null) compareResult = 1;
                    else if (valA is IComparable comparableA) compareResult = comparableA.CompareTo(valB);
                    else compareResult = string.Compare(valA.ToString(), valB.ToString(), StringComparison.Ordinal);

                    return state.SortAscending ? compareResult : -compareResult;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during DataGrid sort: {ex.Message}");
                state.SortColumnIndex = -1; // Disable sorting if it fails
            }
        }


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
        float totalContentHeight = items.Count * rowHeight;

        // Determine scrollbar visibility and available content view size
        bool vScrollVisible = totalContentHeight > contentBounds.Height;
        float viewWidth = contentBounds.Width - (vScrollVisible ? scrollbarThickness : 0);

        // Auto-size columns if requested
        if (autoSizeColumns)
        {
            float totalInitialWidth = columns.Sum(c => c.InitialWidth);
            if (totalInitialWidth > 0 && viewWidth > 0)
            {
                float scaleFactor = viewWidth / totalInitialWidth;
                state.ColumnWidths.Clear();
                float accumulatedWidth = 0;
                for (int i = 0; i < columns.Count; i++)
                {
                    // For all but the last column, calculate and round.
                    if (i < columns.Count - 1)
                    {
                        float newWidth = (float)Math.Round(columns[i].InitialWidth * scaleFactor);
                        state.ColumnWidths.Add(newWidth);
                        accumulatedWidth += newWidth;
                    }
                    else // For the last column, use the remaining space to avoid rounding errors.
                    {
                        state.ColumnWidths.Add(Math.Max(20, viewWidth - accumulatedWidth)); // Ensure a minimum width
                    }
                }
            }
        }

        float totalContentWidth = state.ColumnWidths.Sum();
        bool hScrollVisible = !autoSizeColumns && totalContentWidth > contentBounds.Width;
        float viewHeight = contentBounds.Height - (hScrollVisible ? scrollbarThickness : 0);


        // --- Draw Main Background ---
        Context.Renderer.DrawBox(gridBounds, gridStyle);

        // --- Draw Header ---
        DrawDataGridHeader(intId, state, columns, headerBounds, headerStyle, !autoSizeColumns);

        // --- Handle Scrolling and Draw Rows ---
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

        // Find the index of the selected item within the newly sorted list for display.
        int displayIndex = (currentSelectedItem != null) ? sortedItems.IndexOf(currentSelectedItem) : -1;
        int displayIndexBeforeDraw = displayIndex;

        // Define and push clip rect for content area
        var contentClipRect = new Rect(contentBounds.X, contentBounds.Y, viewWidth, viewHeight);
        Context.Layout.PushClipRect(contentClipRect);
        Context.Renderer.PushClipRect(contentClipRect);

        DrawDataGridRows(intId, sortedItems, columns, state, contentBounds, new Vector2(viewWidth, viewHeight), rowHeight, ref displayIndex, rowStyle, trimCellText);

        // Pop clip rects
        Context.Renderer.PopClipRect();
        Context.Layout.PopClipRect();

        // After drawing, if the user clicked a different row, the displayIndex will have changed.
        // We need to find the new item in the original list and update the caller's selectedIndex.
        if (displayIndex != displayIndexBeforeDraw)
        {
            if (displayIndex >= 0 && displayIndex < sortedItems.Count)
            {
                T newSelectedItem = sortedItems[displayIndex];
                // Find this item in the original list to update the external index.
                // This is a linear scan, which is acceptable for moderately sized lists.
                int newOriginalIndex = -1;
                for (int i = 0; i < items.Count; i++)
                {
                    // Using object.Equals for robust comparison, especially with value types.
                    if (object.Equals(items[i], newSelectedItem))
                    {
                        newOriginalIndex = i;
                        break;
                    }
                }
                selectedIndex = newOriginalIndex;
            }
            else
            {
                selectedIndex = -1; // Selection was cleared.
            }
        }


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

    private static void DrawDataGridHeader(int id, DataGridState state, IReadOnlyList<DataGridColumn> columns, Rect headerBounds, ButtonStyle style, bool allowColumnResize)
    {
        var input = Context.InputState;
        float currentX = headerBounds.X - state.ScrollOffset.X;

        Context.Renderer.PushClipRect(headerBounds);

        // Create a temporary ButtonStylePack for the header for hover feedback
        var headerTheme = new ButtonStylePack { Roundness = 0, BorderLength = 0 };
        headerTheme.Normal = new ButtonStyle(style);
        headerTheme.Pressed = new ButtonStyle(style);
        headerTheme.Hover = new ButtonStyle(style);
        // Make hover slightly brighter for feedback
        var hoverColor = headerTheme.Hover.FillColor;
        hoverColor.R = (byte)Math.Min(255, (int)(hoverColor.R * 1.1f));
        hoverColor.G = (byte)Math.Min(255, (int)(hoverColor.G * 1.1f));
        hoverColor.B = (byte)Math.Min(255, (int)(hoverColor.B * 1.1f));
        headerTheme.Hover.FillColor = hoverColor;

        for (int i = 0; i < columns.Count; i++)
        {
            float colWidth = state.ColumnWidths[i];
            var colHeaderBounds = new Rect(currentX, headerBounds.Y, colWidth, headerBounds.Height);

            // Header click for sorting using a proper button primitive
            int headerId = HashCode.Combine(id, "header", i);
            string headerText = columns[i].HeaderText;
            if (state.SortColumnIndex == i)
            {
                headerText += state.SortAscending ? " ▲" : " ▼";
            }

            // Use DrawButtonPrimitive with a high layer to prevent click-through
            bool wasHeaderClicked = DrawButtonPrimitive(
                id: headerId,
                bounds: colHeaderBounds,
                text: headerText,
                theme: headerTheme,
                disabled: false,
                textAlignment: new Alignment(HAlignment.Left, VAlignment.Center),
                clickMode: DirectUI.Button.ActionMode.Press, // Use Press mode for fair arbitration
                clickBehavior: DirectUI.Button.ClickBehavior.Left,
                textOffset: new Vector2(5, 0),
                isActive: false,
                layer: 5 // High layer to win against grid rows
            );

            if (wasHeaderClicked)
            {
                if (state.SortColumnIndex == i)
                {
                    state.SortAscending = !state.SortAscending;
                }
                else
                {
                    state.SortColumnIndex = i;
                    state.SortAscending = true;
                }
            }

            if (allowColumnResize)
            {
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
                    // Use a higher layer for the resize handle than the header button itself
                    if (State.TrySetActivePress(handleId, 10))
                    {
                        state.ResizingColumnIndex = i;
                        state.DragStartMouseX = input.MousePosition.X;
                        state.ColumnResizeStartWidth = state.ColumnWidths[i];
                    }
                }
            }
            currentX += colWidth;
        }

        Context.Renderer.PopClipRect();
    }

    private static string TrimTextWithEllipsis(string text, float maxWidth, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0) return string.Empty;

        var textService = UI.Context.TextService;
        const string ellipsis = "...";

        var fullSize = textService.MeasureText(text, style);
        if (fullSize.X <= maxWidth) return text;

        var ellipsisSize = textService.MeasureText(ellipsis, style);
        if (ellipsisSize.X > maxWidth) return string.Empty; // Not even ellipsis fits

        // Use a copy of the string to trim
        string trimmed = text;
        while (trimmed.Length > 0)
        {
            var tempText = trimmed + ellipsis;
            if (textService.MeasureText(tempText, style).X <= maxWidth)
            {
                return tempText;
            }
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        return ellipsis; // If nothing else fits
    }

    private static void DrawDataGridRows<T>(int id, IReadOnlyList<T> items, IReadOnlyList<DataGridColumn> columns, DataGridState state, Rect contentBounds, Vector2 viewSize, float rowHeight, ref int selectedIndex, ButtonStylePack rowStyle, bool trimCellText)
    {
        var input = Context.InputState;

        int firstVisibleRow = (int)Math.Floor(state.ScrollOffset.Y / rowHeight);
        int visibleRowCount = (int)Math.Ceiling(viewSize.Y / rowHeight) + 1;
        int lastVisibleRow = Math.Min(items.Count - 1, firstVisibleRow + visibleRowCount);

        for (int i = firstVisibleRow; i <= lastVisibleRow; i++)
        {
            var item = items[i];
            float rowY = contentBounds.Y + (i * rowHeight) - state.ScrollOffset.Y;
            var rowBounds = new Rect(contentBounds.X, rowY, viewSize.X, rowHeight); // Use view width for selection
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

                string textToDraw = cellText;
                var textMargin = new Vector2(5, 0);

                if (trimCellText)
                {
                    float availableWidth = cellBounds.Width - (textMargin.X * 2);
                    if (availableWidth > 0)
                    {
                        var measuredSize = Context.TextService.MeasureText(cellText, rowStyle.Current);
                        if (measuredSize.X > availableWidth)
                        {
                            textToDraw = TrimTextWithEllipsis(cellText, availableWidth, rowStyle.Current);
                        }
                    }
                }

                Context.Renderer.PushClipRect(cellBounds); // Clip text to cell
                DrawTextPrimitive(cellBounds, textToDraw, rowStyle.Current, new Alignment(HAlignment.Left, VAlignment.Center), textMargin);
                Context.Renderer.PopClipRect();

                currentX += colWidth;
            }
        }
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