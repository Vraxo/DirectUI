using System.Numerics;
using DirectUI;
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

    private void DrawColumnInterior(KanbanColumn column, float innerContentLogicalWidth)
    {
        DrawColumnHeader(column, innerContentLogicalWidth);
        DrawColumnTasks(column, innerContentLogicalWidth);
        DrawAddTaskButton(column, innerContentLogicalWidth);
    }

    public void DrawColumnContent(KanbanColumn column, float logicalColumnWidth)
    {
        var contentPadding = 15f;
        var gap = 10f;

        // The style for the column background, previously in a separate method.
        BoxStyle columnStyle = new()
        {
            FillColor = new Color(30, 30, 30, 255),
            Roundness = 0.1f,
            BorderLength = 0
        };

        // Use the new AutoPanel widget. It handles the two-pass layout, background drawing,
        // content layout setup, and advancing the parent layout automatically.
        var columnBounds = UI.AutoPanel(
            id: column.Id,
            logicalWidth: logicalColumnWidth,
            drawContent: (innerWidth) => DrawColumnInterior(column, innerWidth),
            style: columnStyle,
            padding: new Vector2(contentPadding, contentPadding),
            gap: gap
        );

        // After the panel is fully drawn and its final bounds are known,
        // update the drop target for the entire column area.
        int finalTaskIndex = column.Tasks.Count;
        _dragDropHandler.UpdateDropTargetForColumn(column, columnBounds, finalTaskIndex);
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
                TaskRenderer.DrawDragPlaceholder(task, innerContentLogicalWidth);
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


        if (UI.Button(
            id: column.Id + "_add_task",
            text: "+ Add Task",
            size: new(innerContentLogicalWidth, 40),
            theme: addTaskTheme))
        {
            _modalManager.OpenAddTaskModal(column);
        }
    }
}