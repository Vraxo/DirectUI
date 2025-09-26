using System.Numerics;
using DirectUI;
using DirectUI.Animation;
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

    private void DrawColumnBackground(Vector2 position, Vector2 size)
    {
        var columnBgColor = new Color(30, 30, 30, 255); // #1e1e1e
        var columnStyle = new BoxStyle { FillColor = columnBgColor, Roundness = 0.1f, BorderLength = 0 };
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(position.X, position.Y, size.X, size.Y), columnStyle);
    }

    public void DrawColumnContent(KanbanColumn column, float logicalColumnWidth)
    {
        var scale = UI.Context.UIScale;

        // 1. Calculate this column's total height, which includes its outer padding.
        float myTotalPhysicalHeight = KanbanLayoutCalculator.CalculateColumnContentHeight(column, scale);
        var myLogicalPosition = UI.Context.Layout.GetCurrentPosition();
        var myPhysicalPosition = myLogicalPosition * scale;

        // 2. Draw the background for the entire column area using PHYSICAL dimensions.
        DrawColumnBackground(myPhysicalPosition, new Vector2(logicalColumnWidth * scale, myTotalPhysicalHeight));

        // 3. Begin a VBox for the column's inner content. It operates in LOGICAL space.
        var contentPadding = 15f;
        var contentStartPosition = myLogicalPosition + new Vector2(contentPadding, contentPadding);
        UI.BeginVBoxContainer(column.Id, contentStartPosition, gap: 10f);

        // 4. Draw the actual widgets inside the column using LOGICAL dimensions.
        var contentWidth = logicalColumnWidth - contentPadding * 2;
        var titleStyle = new ButtonStyle { FontColor = new Color(224, 224, 224, 255), FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold };
        UI.Text(column.Id + "_title", column.Title, new Vector2(contentWidth, 30), titleStyle, new Alignment(HAlignment.Center, VAlignment.Center));

        // Separator vertical padding is 5, thickness 2. Total logical height = 12.
        UI.Separator(contentWidth, 2, 5, new Color(51, 51, 51, 255));

        int currentTaskIndex = 0;
        foreach (var task in column.Tasks)
        {
            _taskRenderer.DrawDropIndicator(column, currentTaskIndex, contentWidth);
            if (task != _dragDropHandler.DraggedTask)
            {
                _taskRenderer.DrawTaskWidget(column, task, currentTaskIndex, contentWidth);
            }
            else
            {
                _taskRenderer.DrawDragPlaceholder(task, contentWidth);
            }
            currentTaskIndex++;
        }
        // Draw a final drop indicator at the end of the list
        _taskRenderer.DrawDropIndicator(column, currentTaskIndex, contentWidth);
        // After iterating through tasks, check for a drop in an empty or final position
        var columnBounds = new Vortice.Mathematics.Rect(myPhysicalPosition.X, myPhysicalPosition.Y, logicalColumnWidth * scale, myTotalPhysicalHeight);
        _dragDropHandler.UpdateDropTargetForColumn(column, columnBounds, currentTaskIndex);

        if (column.Id == "todo")
        {
            var addTaskTheme = new ButtonStylePack();
            addTaskTheme.Normal.FillColor = Colors.Transparent;
            addTaskTheme.Normal.BorderColor = new Color(51, 51, 51, 255);

            addTaskTheme.Hover.FillColor = DefaultTheme.Accent;
            addTaskTheme.Hover.BorderColor = DefaultTheme.Accent;
            addTaskTheme.Hover.Scale = new Vector2(0.95f, 0.95f); // Scale down on hover

            addTaskTheme.Pressed.Scale = new Vector2(0.9f, 0.9f); // Scale down further on press

            if (UI.Button(column.Id + "_add_task", "+ Add Task", size: new Vector2(contentWidth, 40), theme: addTaskTheme, animation: new AnimationInfo()))
            {
                _modalManager.OpenAddTaskModal(column);
            }
        }

        // Add flexible space to push content to the top
        var vboxState = UI.Context.Layout.PeekContainer() as VBoxContainerState;
        if (vboxState != null)
        {
            float actualContentLogicalHeight = vboxState.GetAccumulatedSize().Y;
            float desiredContentLogicalHeight = (myTotalPhysicalHeight / scale) - (contentPadding * 2);

            if (desiredContentLogicalHeight > actualContentLogicalHeight)
            {
                float paddingAmount = desiredContentLogicalHeight - actualContentLogicalHeight;
                UI.Context.Layout.AdvanceLayout(new Vector2(0, paddingAmount));
            }
        }

        UI.EndVBoxContainer();
    }
}