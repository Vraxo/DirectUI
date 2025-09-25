using System.Collections.Generic;
using System.Numerics;
using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;

namespace Bankan.Modals;

public class AddTaskModal
{
    public string TaskText { get; private set; } = "";
    public string SelectedColorHex { get; private set; } = "";

    private readonly IWindowHost _windowHost;
    private readonly List<string> _availableTaskColors;

    public AddTaskModal(IWindowHost windowHost, List<string> availableColors)
    {
        _windowHost = windowHost;
        _availableTaskColors = availableColors;
    }

    public void Open()
    {
        TaskText = "";
        SelectedColorHex = _availableTaskColors.Count > 0 ? _availableTaskColors[0] : "#bb86fc";
    }

    public void DrawUI(UIContext context)
    {
        var windowSize = context.Renderer.RenderTargetSize;
        UI.BeginVBoxContainer("add_task_vbox", new Vector2(20, 20), gap: 15f);
        UI.Text("add_task_title", "Create New Task", style: new ButtonStyle { FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });

        string tempText = TaskText;
        UI.InputText("new_task_input", ref tempText, new Vector2(windowSize.X - 40, 40), placeholderText: "Enter task description...");
        TaskText = tempText;

        string tempColor = SelectedColorHex;
        DrawColorSelector("add_task", ref tempColor);
        SelectedColorHex = tempColor;

        UI.Separator(windowSize.X - 40);

        UI.BeginHBoxContainer("add_task_actions_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("save_task_btn", "Save Task", size: new Vector2(120, 40))) _windowHost.ModalWindowService.CloseModalWindow(0);
        if (UI.Button("cancel_task_btn", "Cancel", size: new Vector2(120, 40))) _windowHost.ModalWindowService.CloseModalWindow(1);
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