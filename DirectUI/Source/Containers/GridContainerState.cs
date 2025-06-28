using System.Numerics;

namespace DirectUI;

internal class GridContainerState
{
    internal int Id { get; }
    internal Vector2 StartPosition { get; } // Top-left corner of the grid area
    internal int NumColumns { get; }
    internal Vector2 Gap { get; } // Gap between cells (X and Y)
    internal Vector2 AvailableSize { get; } // Total area the grid can occupy

    // Calculated layout values
    internal float CellWidth { get; }
    internal List<float> RowHeights { get; } = new(); // Store height of each completed row
    internal float CurrentRowMaxHeight { get; set; } = 0f; // Track max height of elements in the current row being built
    internal int CurrentCellIndex { get; set; } = 0; // 0-based index of the next cell to place an element in
    internal Vector2 CurrentDrawPosition { get; set; } // Calculated top-left for the next element

    // Tracking overall bounds occupied by elements
    internal float AccumulatedWidth { get; set; } = 0f;
    internal float AccumulatedHeight { get; set; } = 0f;


    internal GridContainerState(int id, Vector2 startPosition, Vector2 availableSize, int numColumns, Vector2 gap)
    {
        Id = id;
        StartPosition = startPosition;
        AvailableSize = availableSize;
        NumColumns = Math.Max(1, numColumns); // Ensure at least one column
        Gap = gap;

        // Calculate fixed cell width based on available space and gaps
        float totalHorizontalGap = Math.Max(0, NumColumns - 1) * Gap.X;
        float widthForCells = AvailableSize.X - totalHorizontalGap;
        CellWidth = (NumColumns > 0) ? Math.Max(0, widthForCells / NumColumns) : 0;

        // Initial position is the start position
        CurrentDrawPosition = startPosition;
        RowHeights.Add(0); // Add initial height for the first row (will be updated)
    }

    internal void MoveToNextCell(Vector2 elementSize)
    {
        // Update max height for the current row
        CurrentRowMaxHeight = Math.Max(CurrentRowMaxHeight, elementSize.Y);

        // Update total occupied width (conservative estimate using cell width)
        int currentCol = CurrentCellIndex % NumColumns;
        float currentWidth = (currentCol + 1) * CellWidth + currentCol * Gap.X;
        AccumulatedWidth = Math.Max(AccumulatedWidth, currentWidth);


        CurrentCellIndex++;
        int nextCol = CurrentCellIndex % NumColumns;

        if (nextCol == 0) // Moved to the start of a new row
        {
            // Finalize the height of the completed row
            RowHeights[^1] = CurrentRowMaxHeight; // Update last row's height
            AccumulatedHeight += CurrentRowMaxHeight + (RowHeights.Count > 1 ? Gap.Y : 0); // Add height and gap (if not first row)

            // Reset for the new row
            CurrentRowMaxHeight = 0f;
            RowHeights.Add(0); // Add placeholder height for the new row

            // Calculate Y position for the new row
            float newY = StartPosition.Y;
            for (int i = 0; i < RowHeights.Count - 1; i++) // Sum heights and gaps of completed rows
            {
                newY += RowHeights[i] + Gap.Y;
            }

            // Set position for the first cell of the new row
            CurrentDrawPosition = new Vector2(StartPosition.X, newY);

        }
        else // Moving to the next column in the same row
        {
            // Calculate X position for the next cell
            float currentX = StartPosition.X + nextCol * (CellWidth + Gap.X);
            CurrentDrawPosition = new Vector2(currentX, CurrentDrawPosition.Y); // Y stays the same
        }
    }

    internal Vector2 GetTotalOccupiedSize()
    {
        // Calculate final height including the current row being built
        float finalHeight = AccumulatedHeight + CurrentRowMaxHeight;
        return new Vector2(AccumulatedWidth, finalHeight);
    }
}