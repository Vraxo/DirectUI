using System.Numerics;
using DirectUI;
using Vortice.Mathematics;

namespace Daw.Views;

public class PianoRollToolbarView
{
    public void Draw(Rect viewArea, ref PianoRollTool currentTool)
    {
        UI.Context.Renderer.DrawBox(viewArea, new BoxStyle { FillColor = DawTheme.PanelBackground, Roundness = 0 });

        UI.BeginHBoxContainer("pianoroll_toolbar", viewArea.TopLeft + new Vector2(5, 5), 5);

        // Select Tool Button
        bool isSelectActive = currentTool == PianoRollTool.Select;
        if (UI.Button("tool_select", "↗", new Vector2(30, 30), DawTheme.LoopToggleStyle, isActive: isSelectActive))
        {
            currentTool = PianoRollTool.Select;
        }

        // Pencil Tool Button
        bool isPencilActive = currentTool == PianoRollTool.Pencil;
        if (UI.Button("tool_pencil", "✎", new Vector2(30, 30), DawTheme.LoopToggleStyle, isActive: isPencilActive))
        {
            currentTool = PianoRollTool.Pencil;
        }

        UI.EndHBoxContainer();
    }
}
