using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Core;

namespace Bankan.Rendering;

public static class LayoutCalculator
{
    public static float CalculateColumnContentHeight(KanbanColumn column, float scale)
    {
        // This is a layout calculation, so we use LOGICAL units for everything internally
        // and only scale at the end.
        float logicalColumnWidth = 350f;
        float logicalContentPadding = 15f;
        float logicalGap = 10f;
        float logicalTasksInnerWidth = logicalColumnWidth - (logicalContentPadding * 2);

        float height = 0;
        height += logicalContentPadding; // Top padding
        height += 30f + logicalGap;      // Title + gap
        height += 12f + logicalGap;      // Separator (2 thickness + 5*2 padding) + gap

        if (column.Tasks.Any())
        {
            // For text measurement, we need to use the physical (scaled) font size.
            var measurementStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 * scale };
            foreach (var task in column.Tasks)
            {
                // Task text area has padding (15 left/right)
                float taskTextAreaWidth = logicalTasksInnerWidth - 30f;
                var wrappedLayout = UI.Context.TextService.GetTextLayout(
                    task.Text,
                    measurementStyle,
                    new Vector2(taskTextAreaWidth * scale, float.MaxValue),
                    new Alignment(HAlignment.Left, VAlignment.Top));

                // The layout size is physical, so we unscale it to get the logical height.
                // Then add the logical padding for the task widget.
                height += (wrappedLayout.Size.Y / scale) + 30f;
                height += logicalGap;
            }
            height -= logicalGap;
        }

        if (column.Id == "todo")
        {
            height += logicalGap;
            height += 40f; // Add task button
        }

        height += logicalContentPadding; // Bottom padding

        // Return the final PHYSICAL height
        return height * scale;
    }
}