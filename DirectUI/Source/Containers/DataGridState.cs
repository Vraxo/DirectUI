// DirectUI/Source/Containers/DataGridState.cs
using System.Collections.Generic;
using System.Numerics;

namespace DirectUI;

internal enum SortDirection { Ascending, Descending }

internal class DataGridState
{
    public int Id { get; set; }
    public List<float> ColumnWidths { get; set; } = new();
    public Vector2 ScrollOffset { get; set; } = Vector2.Zero;
    public int ResizingColumnIndex { get; set; } = -1;

    // State for column resizing drag operation
    public float ColumnResizeStartWidth { get; set; }
    public float DragStartMouseX { get; set; }

    // New properties for sorting
    public int SortColumnIndex { get; set; } = -1;
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;
}