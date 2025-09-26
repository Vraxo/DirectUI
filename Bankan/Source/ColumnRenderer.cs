using System.Numerics;
using DirectUI;
using DirectUI.Animation;
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
        float columnPhysicalHeight = LayoutCalculator.CalculateColumnContentHeight(column, scale);
        Vector2 columnLogicalPosition = UI.Context.Layout.GetCurrentPosition();
        Vector2 columnPhysicalPosition = columnLogicalPosition * scale;

        DrawColumnBackground(
            columnPhysicalPosition,
            new(logicalColumnWidth * scale, columnPhysicalHeight));

        float contentPadding = 15f;
        Vector2 contentStartPosition = columnLogicalPosition + new Vector2(contentPadding, contentPadding);

        UI.BeginVBoxContainer(
            column.Id,
            contentStartPosition,
            gap: 10f);

        float innerContentLogicalWidth = logicalColumnWidth - contentPadding * 2;

        DrawColumnHeader(column, innerContentLogicalWidth);
        DrawColumnTasks(column, innerContentLogicalWidth);

        Vortice.Mathematics.Rect columnBoundsForDropTarget = new(columnPhysicalPosition.X, columnPhysicalPosition.Y, logicalColumnWidth * scale, columnPhysicalHeight);
        int finalTaskIndex = column.Tasks.Count;

        _dragDropHandler.UpdateDropTargetForColumn(column, columnBoundsForDropTarget, finalTaskIndex);

        DrawAddTaskButton(column, innerContentLogicalWidth);
        AddFlexibleSpaceToFillColumn(columnPhysicalHeight, contentPadding);

        UI.EndVBoxContainer();
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

    private static void AddFlexibleSpaceToFillColumn(float columnPhysicalHeight, float contentPadding)
    {
        float scale = UI.Context.UIScale;

        if (UI.Context.Layout.PeekContainer() is not VBoxContainerState vboxState)
        {
            return;
        }

        float actualContentLogicalHeight = vboxState.GetAccumulatedSize().Y;
        float desiredContentLogicalHeight = (columnPhysicalHeight / scale) - (contentPadding * 2);

        if (desiredContentLogicalHeight <= actualContentLogicalHeight)
        {
            return;
        }

        float paddingAmount = desiredContentLogicalHeight - actualContentLogicalHeight;
        UI.Context.Layout.AdvanceLayout(new(0, paddingAmount));
    }
}