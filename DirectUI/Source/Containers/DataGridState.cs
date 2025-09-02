// DirectUI/Source/Containers/DataGridState.cs
using System.Collections.Generic;
using System.Numerics;

namespace DirectUI;

internal class DataGridState
{
    public int Id { get; set; }
    public List<float> ColumnWidths { get; set; } = new();
    public Vector2 ScrollOffset { get; set; } = Vector2.Zero;
    public int ResizingColumnIndex { get; set; } = -1;

    // New properties for sorting
    public int SortColumnIndex { get; set; } = -1;
    public bool SortAscending { get; set; } = true;

    // State for column resizing drag operation
    public float ColumnResizeStartWidth { get; set; }
    public float DragStartMouseX { get; set; }
}