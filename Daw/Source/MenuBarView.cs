using System.Numerics;
using DirectUI;
using Vortice.Mathematics;

namespace Daw.Views;

public class MenuBarView
{
    public enum FileAction { None, Save, Load, Export }

    private FileAction _requestedAction = FileAction.None;

    public void Draw(Vector2 position)
    {
        _requestedAction = FileAction.None;

        UI.BeginHBoxContainer("menubar", position, 0);

        // Using a dropdown menu style would be the next step.
        // For now, these are direct action buttons.
        DrawMenuButton("File", new[] { "Save", "Load", "Export MIDI" }, HandleFileAction);
        DrawMenuButton("Edit", new[] { "Undo", "Redo" });
        DrawMenuButton("View", new[] { "Zoom In", "Zoom Out" });

        UI.EndHBoxContainer();
    }

    private void HandleFileAction(string item)
    {
        _requestedAction = item switch
        {
            "Save" => FileAction.Save,
            "Load" => FileAction.Load,
            "Export MIDI" => FileAction.Export,
            _ => FileAction.None
        };
    }

    public FileAction GetAction()
    {
        return _requestedAction;
    }

    private void DrawMenuButton(string text, string[] items, Action<string>? onItemSelected = null)
    {
        var buttonId = $"menu_button_{text}";
        var menuButtonStyle = new ButtonStylePack
        {
            Roundness = 0,
            BorderLength = 0
        };
        menuButtonStyle.Normal.FillColor = Colors.Transparent;
        menuButtonStyle.Hover.FillColor = DawTheme.ControlFillHover;

        if (UI.Button(buttonId, text, new Vector2(0, 30), autoWidth: true, textMargin: new Vector2(10, 0), theme: menuButtonStyle))
        {
            // Placeholder for future dropdown logic. For now, top-level buttons do nothing.
            // A real implementation would open a popup here.
            // For simplicity, we make the first item the default action.
            if (onItemSelected != null && items.Length > 0)
            {
                onItemSelected(items[0]);
            }
        }
    }
}