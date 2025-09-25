using System.Collections.Generic;
using System.Numerics;
using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;

namespace Bankan.Modals;

public class EditTaskModal
{
    public string TaskText { get; private set; } = "";
    public string SelectedColorHex { get; private set; } = "";
    public KanbanTask? TaskToEdit { get; private set; }

    private readonly IWindowHost _windowHost;
    private readonly List<string> _availableTaskColors;

    public EditTaskModal(IWindowHost windowHost, List<string> availableColors)
    {
        _windowHost = windowHost;
        _availableTaskColors = availableColors;
    }

    public void Open(KanbanTask task)
    {
        TaskToEdit = task;
        TaskText = task.Text;
        SelectedColorHex = task.ColorHex;
    }

    public void DrawUI(UIContext context)
    {
        var windowSize = context.Renderer.RenderTargetSize;
        UI.BeginVBoxContainer("edit_task_vbox", new Vector2(20, 20), gap: 15f);
        UI.Text("edit_task_title", "Edit Task", style: new ButtonStyle { FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });

        string tempText = TaskText;
        UI.InputText("edit_task_input", ref tempText, new Vector2(windowSize.X - 40, 40), placeholderText: "Enter task description...");
        TaskText = tempText;

        string tempColor = SelectedColorHex;
        DrawColorSelector("edit_task", ref tempColor);
        SelectedColorHex = tempColor;

        UI.Separator(windowSize.X - 40);

        UI.BeginHBoxContainer("edit_task_actions_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("save_edit_btn", "Save Changes", size: new Vector2(140, 40))) _windowHost.ModalWindowService.CloseModalWindow(0);
        var deleteTheme = new ButtonStylePack { Normal = { FillColor = new Color(207, 102, 121, 255) }, Hover = { FillColor = new Color(176, 81, 98, 255) } };
        if (UI.Button("delete_task_btn", "Delete Task", size: new Vector2(120, 40), theme: deleteTheme)) _windowHost.ModalWindowService.CloseModalWindow(2);
        UI.EndHBoxContainer();
        UI.EndVBoxContainer();
    }

    private void DrawColorSelector(string idPrefix, ref string selectedColorHex)
    {
        UI.BeginHBoxContainer($"{idPrefix}_color_selector_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        foreach (var colorHex in _availableTaskColors)
        {
            var swatchTheme = new ButtonStylePack { Roundness = 0.5f, Normal = { FillColor = new KanbanTask { ColorHex = colorHex }.Color, BorderColor = colorHex == selectedColorHex ? Colors.White : Colors.Transparent, BorderLength = 3f } };
            if (UI.Button($"{idPrefix}_swatch_{colorHex}", "", size: new Vector2(30, 30), theme: swatchTheme))
            {
                selectedColorHex = colorHex;
            }
        }
        UI.EndHBoxContainer();
    }
}