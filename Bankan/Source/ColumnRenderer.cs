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

    /// <summary>
    /// Draws the shared UI elements that make up the inside of a column.
    /// This logic is used for both the calculation pass and the final drawing pass.
    /// </summary>
    private void DrawColumnInterior(KanbanColumn column, float innerContentLogicalWidth)
    {
        DrawColumnHeader(column, innerContentLogicalWidth);
        DrawColumnTasks(column, innerContentLogicalWidth);
        DrawAddTaskButton(column, innerContentLogicalWidth);
    }

    public void DrawColumnContent(KanbanColumn column, float logicalColumnWidth)
    {
        var scale = UI.Context.UIScale;
        var contentPadding = 15f;
        var gap = 10f;
        var innerContentLogicalWidth = logicalColumnWidth - contentPadding * 2;

        // --- 1. Calculation Pass ---
        // Use the built-in layout calculation feature to determine the content's size
        // by running the shared drawing logic in a "dry run" mode.
        var contentSize = UI.CalculateLayout(() =>
        {
            UI.BeginVBoxContainer(
                column.Id + "_calc",
                Vector2.Zero,
                gap: gap);

            DrawColumnInterior(column, innerContentLogicalWidth);

            UI.EndVBoxContainer();
        });

        // The total size of the column includes its internal padding.
        var columnLogicalHeight = contentSize.Y + contentPadding * 2;
        var columnLogicalSize = new Vector2(logicalColumnWidth, columnLogicalHeight);

        // --- 2. Drawing Pass ---
        var columnLogicalPosition = UI.Context.Layout.GetCurrentPosition();
        var columnPhysicalPosition = columnLogicalPosition * scale;

        DrawColumnBackground(
            columnPhysicalPosition,
            new Vector2(columnLogicalSize.X * scale, columnLogicalSize.Y * scale));

        var contentStartPosition = columnLogicalPosition + new Vector2(contentPadding, contentPadding);

        // Begin a VBox for arranging the content. It will NOT advance the parent layout itself.
        UI.BeginVBoxContainer(
            column.Id,
            contentStartPosition,
            gap: gap);

        // Call the shared logic again, this time for actual rendering.
        DrawColumnInterior(column, innerContentLogicalWidth);

        // Update the drop target for the entire column area, now that we know its final physical size.
        var columnBoundsForDropTarget = new Vortice.Mathematics.Rect(columnPhysicalPosition.X, columnPhysicalPosition.Y, columnLogicalSize.X * scale, columnLogicalSize.Y * scale);
        int finalTaskIndex = column.Tasks.Count;
        _dragDropHandler.UpdateDropTargetForColumn(column, columnBoundsForDropTarget, finalTaskIndex);

        // End the container without advancing the parent (the HBox).
        UI.EndVBoxContainer(advanceParentLayout: false);

        // Manually advance the parent HBox layout by the full calculated size of this column widget.
        UI.Context.Layout.AdvanceLayout(columnLogicalSize);
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