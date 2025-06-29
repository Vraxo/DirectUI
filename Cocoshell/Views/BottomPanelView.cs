namespace DirectUI;

public class BottomPanelView
{
    public static void Draw()
    {
        UI.PushStyleVar(StyleVar.FrameRounding, 0f);
        
        if (UI.Button("bottom_button", "Bottom Panel Button"))
        {

        }

        UI.PopStyleVar();
    }
}