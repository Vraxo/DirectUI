using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Animation;
using DirectUI.Core;
using DirectUI.Drawing;
using DirectUI.Styling;

namespace Bankan.Rendering;

public class ColumnRenderer
{
    private readonly TaskRenderer _taskRenderer;
    private readonly DragDropHandler _dragDropHandler;
    private readonly ModalManager _modalManager;

    public ColumnRenderer(TaskRenderer taskRenderer, DragDropHandler dragDropHandler, ModalManager modalManager)
    {
        _taskRenderer = taskRenderer;
        _dragDropHandler = dragDropHandler;
        _modalManager = modalManager;
    }

    public void DrawColumnContent(KanbanColumn column, float logicalColumnWidth)
    {
        float scale = UI.Context.UIScale;
        float columnPhysicalHeight = CalculateColumnContentHeight(column, logicalColumnWidth, scale);
        Vector2 columnLogicalPosition = UI.Context.Layout.GetCurrentPosition();
        Vector2 columnPhysicalPosition = columnLogicalPosition * scale;

        DrawColumnBackground(
            columnPhysicalPosition,
            new(logicalColumnWidth * scale, columnPhysicalHeight));

        float contentPadding = 15f;
        Vector2 contentStartPosition = columnLogicalPosition + new Vector2(contentPadding, contentPadding);

        // Calculate the minimum logical height for the VBox content area.
        // This ensures the container reports a height that matches the background, even if content is shorter.
        float columnLogicalHeight = columnPhysicalHeight / scale;
        float minContentLogicalHeight = columnLogicalHeight - (contentPadding * 2);

        UI.BeginVBoxContainer(
            column.Id,
            contentStartPosition,
            gap: 10f,
            minSize: new Vector2(0, minContentLogicalHeight));

        float innerContentLogicalWidth = logicalColumnWidth - contentPadding * 2;

        DrawColumnHeader(column, innerContentLogicalWidth);
        DrawColumnTasks(column, innerContentLogicalWidth);

        Vortice.Mathematics.Rect columnBoundsForDropTarget = new(columnPhysicalPosition.X, columnPhysicalPosition.Y, logicalColumnWidth * scale, columnPhysicalHeight);
        int finalTaskIndex = column.Tasks.Count;

        _dragDropHandler.UpdateDropTargetForColumn(column, columnBoundsForDropTarget, finalTaskIndex);

        DrawAddTaskButton(column, innerContentLogicalWidth);

        UI.EndVBoxContainer();
    }

    private static float CalculateColumnContentHeight(KanbanColumn column, float logicalColumnWidth, float scale)
    {
        // This is a layout calculation, so we use LOGICAL units for everything internally
        // and only scale at the end.
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


    private static void DrawColumnBackground(Vector2 position, Vector2 size)
    {
        Color columnBgColor = new(30, 30, 30, 255);

        BoxStyle columnStyle = new()
        {
            FillColor =
            columnBgColor,
            Roundness = 0.1f,
            BorderLength = 0
        };

        UI.Context.Renderer.DrawBox(
            new(position.X, position.Y, size.X, size.Y),
            columnStyle);
    }

    private static void DrawColumnHeader(KanbanColumn column, float innerContentLogicalWidth)
    {
        ButtonStyle titleStyle = new()
        {
            FontColor = new(224, 224, 224, 255),
            FontSize = 18,
            FontWeight = Vortice.DirectWrite.FontWeight.SemiBold
        };

        UI.Text(
            column.Id + "_title",
            column.Title,
            new(innerContentLogicalWidth, 30),
            titleStyle,
            new(HAlignment.Center, VAlignment.Center));

        UI.Separator(
            innerContentLogicalWidth,
            2,
            5,
            new(51, 51, 51, 255));
    }

    private void DrawColumnTasks(KanbanColumn column, float innerContentLogicalWidth)
    {
        int currentTaskIndex = 0;

        foreach (Task task in column.Tasks)
        {
            _taskRenderer.DrawDropIndicator(column, currentTaskIndex, innerContentLogicalWidth);

            if (task != _dragDropHandler.DraggedTask)
            {
                _taskRenderer.DrawTaskWidget(column, task, currentTaskIndex, innerContentLogicalWidth);
            }
            else
            {
                _taskRenderer.DrawDragPlaceholder(task, innerContentLogicalWidth);
            }

            currentTaskIndex++;
        }

        _taskRenderer.DrawDropIndicator(column, currentTaskIndex, innerContentLogicalWidth);
    }

    private void DrawAddTaskButton(KanbanColumn column, float innerContentLogicalWidth)
    {
        if (column.Id != "todo")
        {
            return;
        }

        // --- DEMONSTRATION OF BOTH STYLING METHODS ---

        // Method 1: Data-Driven (from styles.yaml)
        // This is the recommended approach for styles that are shared or need easy tweaking.
        var addTaskTheme = StyleManager.Get<ButtonStylePack>("addTaskButton");


        // Method 2: Code-Driven (defined directly here)
        // This is useful for one-off styles or for developers who prefer to keep everything in C#.
        // To use this, just uncomment this block and comment out the StyleManager line above.
        //ButtonStylePack addTaskTheme = new()
        //{
        //    Animation = new(2.5f), // Fallback animation
        //    Normal =
        //    {
        //        FillColor = Colors.Transparent,
        //        BorderColor = new(51, 51, 51, 255)
        //    },
        //    Hover =
        //    {
        //        FillColor = DefaultTheme.Accent,
        //        BorderColor = DefaultTheme.Accent,
        //        Scale = new(0.95f, 0.95f),
        //        Animation = new(0.25f) // Faster animation TO this state
        //    },
        //    Pressed =
        //    {
        //        Scale = new(0.9f, 0.9f)
        //    }
        //};

        bool clicked = UI.Button(
            id: column.Id + "_add_task",
            text: "+ Add Task",
            size: new(innerContentLogicalWidth, 40),
            theme: addTaskTheme);

        if (clicked)
        {
            _modalManager.OpenAddTaskModal(column);
        }
    }
}