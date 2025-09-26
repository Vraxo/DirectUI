using System.Numerics;
using DirectUI;
using DirectUI.Drawing;

namespace Bankan.Rendering;

public class KanbanColumnRenderer
{
    private readonly KanbanTaskRenderer _taskRenderer;
    private readonly KanbanDragDropHandler _dragDropHandler;
    private readonly KanbanModalManager _modalManager;

    public KanbanColumnRenderer(KanbanTaskRenderer taskRenderer, KanbanDragDropHandler dragDropHandler, KanbanModalManager modalManager)
    {
        _taskRenderer = taskRenderer;
        _dragDropHandler = dragDropHandler;
        _modalManager = modalManager;
    }

    public void DrawColumnContent(KanbanColumn column, float logicalColumnWidth)
    {
        float scale = UI.Context.UIScale;
        float columnPhysicalHeight = KanbanLayoutCalculator.CalculateColumnContentHeight(column, scale);
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

        ButtonStylePack addTaskTheme = new()
        {
            Normal =
            {
                FillColor = Colors.Transparent,
                BorderColor = new(51, 51, 51, 255)
            },
            Hover =
            {
                FillColor = DefaultTheme.Accent,
                BorderColor = DefaultTheme.Accent,
                Scale = new(0.95f, 0.95f)
            },
            Pressed =
            {
                Scale = new(0.9f, 0.9f)
            }
        };

        bool clicked = UI.Button(
            id: column.Id + "_add_task",
            text: "+ Add Task",
            size: new(innerContentLogicalWidth, 40),
            theme: addTaskTheme,
            animation: new());

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