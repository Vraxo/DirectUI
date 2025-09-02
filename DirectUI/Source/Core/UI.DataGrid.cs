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

        // Preserve selected item instance across sorts
        T? selectedItem = default;
        if (selectedIndex >= 0 && selectedIndex < items.Count)
        {
            selectedItem = items[selectedIndex];
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

        // --- Draw Header and check for sort requests ---
        DrawDataGridHeader(intId, state, columns, headerBounds, headerStyle, !autoSizeColumns);

        // --- Sort Data if needed ---
        IReadOnlyList<T> itemsToRender = items;
        List<T>? sortedList = null;
        if (state.SortColumnIndex != -1)
        {
            sortedList = SortData(items, columns, state);
            itemsToRender = sortedList;
        }

        // --- Update selected index after sorting ---
        if (selectedItem != null && sortedList != null)
        {
            selectedIndex = sortedList.IndexOf(selectedItem);
        }
        else if (selectedItem == null)
        {
            selectedIndex = -1;
        }

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

        DrawDataGridRows(intId, itemsToRender, columns, state, contentBounds, new Vector2(viewWidth, viewHeight), rowHeight, ref selectedIndex, rowStyle, trimCellText);

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

    private static bool DrawDataGridHeader(int id, DataGridState state, IReadOnlyList<DataGridColumn> columns, Rect headerBounds, ButtonStyle style, bool allowColumnResize)
    {
        bool sortStateChanged = false;
        var input = Context.InputState;
        float currentX = headerBounds.X - state.ScrollOffset.X;

        Context.Renderer.PushClipRect(headerBounds);

        for (int i = 0; i < columns.Count; i++)
        {
            float colWidth = state.ColumnWidths[i];
            var colHeaderBounds = new Rect(currentX, headerBounds.Y, colWidth, headerBounds.Height);

            // --- Header Button & Sorting Logic ---
            string headerText = columns[i].HeaderText;
            if (state.SortColumnIndex == i)
            {
                headerText += (state.SortDirection == SortDirection.Ascending) ? " ▲" : " ▼";
            }

            var headerButtonId = HashCode.Combine(id, "header", i);
            var headerThemeId = HashCode.Combine(headerButtonId, "theme");
            var headerTheme = State.GetOrCreateElement<ButtonStylePack>(headerThemeId);

            // Set up a theme that uses the base style but has hover/pressed states
            headerTheme.Normal = new ButtonStyle(style);
            headerTheme.Hover.FillColor = DefaultTheme.HoverFill;
            headerTheme.Hover.BorderColor = style.BorderColor;
            headerTheme.Pressed.FillColor = DefaultTheme.Accent;
            headerTheme.Pressed.BorderColor = style.BorderColor;

            // Use DrawButtonPrimitive for click detection. Layer 5 is above rows (1) and below scrollbars (10).
            if (DrawButtonPrimitive(headerButtonId, colHeaderBounds, headerText, headerTheme, false, new Alignment(HAlignment.Left, VAlignment.Center), DirectUI.Button.ActionMode.Release, DirectUI.Button.ClickBehavior.Left, new Vector2(5, 0), layer: 5))
            {
                if (state.SortColumnIndex == i)
                {
                    state.SortDirection = (state.SortDirection == SortDirection.Ascending) ? SortDirection.Descending : SortDirection.Ascending;
                }
                else
                {
                    state.SortColumnIndex = i;
                    state.SortDirection = SortDirection.Ascending;
                }
                sortStateChanged = true;
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
        return sortStateChanged;
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

    private static List<T> SortData<T>(IReadOnlyList<T> items, IReadOnlyList<DataGridColumn> columns, DataGridState state)
    {
        if (state.SortColumnIndex < 0 || state.SortColumnIndex >= columns.Count)
        {
            return items.ToList();
        }

        var sortColumn = columns[state.SortColumnIndex];
        string propertyName = sortColumn.DataPropertyName;

        try
        {
            Func<T, object?> keySelector = item => GetPropertyValue(item, propertyName);

            IEnumerable<T> sortedQuery;
            if (state.SortDirection == SortDirection.Ascending)
            {
                sortedQuery = items.OrderBy(keySelector, Comparer<object>.Default);
            }
            else
            {
                sortedQuery = items.OrderByDescending(keySelector, Comparer<object>.Default);
            }

            return sortedQuery.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during sorting on property '{propertyName}': {ex.Message}");
            return items.ToList();
        }
    }
}