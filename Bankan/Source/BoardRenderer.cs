using System.Numerics;
using Bankan.Rendering;
using DirectUI;

namespace Bankan;

public class BoardRenderer
{
    private readonly KanbanBoard _board;
    private readonly ColumnRenderer _columnRenderer;
    private readonly DragDropHandler _dragDropHandler;

    public BoardRenderer(KanbanBoard board, KanbanSettings settings, ModalManager modalManager, DragDropHandler dragDropHandler)
    {
        _board = board;
        _dragDropHandler = dragDropHandler;
        var taskRenderer = new TaskRenderer(settings, modalManager, dragDropHandler);
        _columnRenderer = new ColumnRenderer(taskRenderer, dragDropHandler, modalManager);
    }

    public void DrawBoard(float columnLogicalWidth, float columnLogicalGap)
    {
        // Reset the drop target state at the beginning of the render pass.
        // This ensures targets are freshly calculated based on this frame's layout.
        _dragDropHandler.PrepareNextFrameTarget();

        // The container system operates in logical (unscaled) space.
        // The HBox starts at whatever position the parent container (the ScrollArea) provides.
        UI.BeginHBoxContainer("board_content_hbox", UI.Context.Layout.GetCurrentPosition(), gap: columnLogicalGap);
        foreach (var column in _board.Columns)
        {
            // The column width passed here is LOGICAL width.
            _columnRenderer.DrawColumnContent(column, columnLogicalWidth);
        }
        UI.EndHBoxContainer();
    }
}