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
        TaskRenderer taskRenderer = new(settings, modalManager, dragDropHandler);
        _columnRenderer = new(taskRenderer, dragDropHandler, modalManager);
    }

    public void DrawBoard(float columnLogicalWidth, float columnLogicalGap)
    {
        _dragDropHandler.PrepareNextFrameTarget();

        UI.BeginHBoxContainer(
            "board_content_hbox",
            UI.Context.Layout.GetCurrentPosition(),
            gap: columnLogicalGap);
        
        foreach (var column in _board.Columns)
        {
            _columnRenderer.DrawColumnContent(column, columnLogicalWidth);
        }

        UI.EndHBoxContainer();
    }
}