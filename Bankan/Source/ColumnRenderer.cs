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
        Vector2 columnLogicalPosition = UI.Context.Layout.GetCurrentPosition();

        Color columnBgColor = new(30, 30, 30, 255);
        BoxStyle columnStyle = new()
        {
            FillColor = columnBgColor,
            Roundness = 0.1f,
            BorderLength = 0
        };

        // The outer VBoxContainer's width is forced, so its background will be the correct size.
        UI.BeginVBoxContainer(
            column.Id,
            columnLogicalPosition,
            background: columnStyle,
            forcedWidth: logicalColumnWidth);

        float contentPadding = 15f;
        float innerContentLogicalWidth = logicalColumnWidth - contentPadding * 2;

        // Use an inner VBox for the actual content, indented by the padding.
        var innerContainerPosition = UI.Context.Layout.GetCurrentPosition() + new Vector2(contentPadding, contentPadding);
        UI.BeginVBoxContainer(
            column.Id + "_inner",
            innerContainerPosition,
            gap: 10f);

        DrawColumnHeader(column, innerContentLogicalWidth);
        DrawColumnTasks(column, innerContentLogicalWidth);

        // --- Drop Target for end-of-column ---
        var innerVboxState = (VBoxContainerState)UI.Context.Layout.PeekContainer();
        var contentLogicalSize = innerVboxState.GetAccumulatedSize();
        var contentLogicalPos = innerVboxState.StartPosition;
        var dropTargetWidth = logicalColumnWidth * scale;
        var dropTargetX = (contentLogicalPos.X - contentPadding) * scale;
        var dropTargetY = contentLogicalPos.Y * scale;
        var dropTargetHeight = contentLogicalSize.Y * scale;
        Vortice.Mathematics.Rect columnContentBounds = new(dropTargetX, dropTargetY, dropTargetWidth, dropTargetHeight);
        int finalTaskIndex = column.Tasks.Count;
        _dragDropHandler.UpdateDropTargetForColumn(column, columnContentBounds, finalTaskIndex);


        DrawAddTaskButton(column, innerContentLogicalWidth);

        // This ends the inner container. Its calculated size will be advanced into the outer container.
        UI.EndVBoxContainer();

        // Add bottom padding by advancing the outer container.
        UI.Context.Layout.AdvanceLayout(new(0, contentPadding));

        // This ends the outer container. It will now draw its background and replay the content.
        UI.EndVBoxContainer();
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