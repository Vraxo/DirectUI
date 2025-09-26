using System.Numerics;
using Bankan.Rendering;
using DirectUI;

namespace Bankan;

public class KanbanBoardRenderer
{
    private readonly KanbanBoard _board;
    private readonly KanbanColumnRenderer _columnRenderer;
    private readonly KanbanDragDropHandler _dragDropHandler;

    public KanbanBoardRenderer(KanbanBoard board, KanbanSettings settings, KanbanModalManager modalManager, KanbanDragDropHandler dragDropHandler)
    {
        _board = board;
        _dragDropHandler = dragDropHandler;
        var taskRenderer = new KanbanTaskRenderer(settings, modalManager, dragDropHandler);
        _columnRenderer = new KanbanColumnRenderer(taskRenderer, dragDropHandler, modalManager);
    }

    public void DrawBoard(Vector2 boardStartPosition, float columnWidth, float columnGap)
    {
        var scale = UI.Context.UIScale;
        // Reset the drop target state at the beginning of the render pass.
        // This ensures targets are freshly calculated based on this frame's layout.
        _dragDropHandler.PrepareNextFrameTarget();

        // The container system operates in logical (unscaled) space.
        // We pass the unscaled start position and gap.
        UI.BeginHBoxContainer("board_content_hbox", boardStartPosition / scale, gap: columnGap / scale);
        foreach (var column in _board.Columns)
        {
            // The column width passed here is LOGICAL width.
            _columnRenderer.DrawColumnContent(column, columnWidth / scale);
        }
        UI.EndHBoxContainer();
    }
}