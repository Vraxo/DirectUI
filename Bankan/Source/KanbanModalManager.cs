using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;

namespace Bankan;

public class KanbanModalManager
{
    private readonly IWindowHost _windowHost;
    private readonly KanbanBoard _board;
    private Action? _saveRequestCallback;

    private string _taskText = "";
    private string _selectedColorHex = "";
    private KanbanTask? _taskToEdit;
    private KanbanColumn? _columnToAddTaskTo;

    private readonly List<string> _availableTaskColors = new() { "#bb86fc", "#ff7597", "#75ffff", "#75ff9f", "#ffdf75" };

    public bool IsModalOpen => _windowHost.ModalWindowService.IsModalWindowOpen;

    public KanbanModalManager(IWindowHost windowHost, KanbanBoard board, Action saveRequestCallback)
    {
        _windowHost = windowHost;
        _board = board;
        _saveRequestCallback = saveRequestCallback;
    }

    public void RequestSave() => _saveRequestCallback?.Invoke();

    public void OpenSettingsModal(KanbanSettings settings)
    {
        if (IsModalOpen) return;
        _windowHost.ModalWindowService.OpenModalWindow("Settings", 400, 250, context => DrawSettingsModalUI(context, settings), _ => RequestSave());
    }

    private void DrawSettingsModalUI(UIContext context, KanbanSettings settings)
    {
        var windowSize = context.Renderer.RenderTargetSize;
        UI.BeginVBoxContainer("settings_vbox", new Vector2(20, 20), gap: 15f);
        UI.Text("settings_title", "Settings", style: new ButtonStyle { FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });
        UI.Separator(windowSize.X - 40);

        UI.Text("color_style_label", "Task Color Style");
        UI.BeginHBoxContainer("color_style_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("style_border_btn", "Border", isActive: settings.ColorStyle == TaskColorStyle.Border)) settings.ColorStyle = TaskColorStyle.Border;
        if (UI.Button("style_bg_btn", "Background", isActive: settings.ColorStyle == TaskColorStyle.Background)) settings.ColorStyle = TaskColorStyle.Background;
        UI.EndHBoxContainer();

        UI.Text("align_label", "Task Text Alignment");
        UI.BeginHBoxContainer("align_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("align_left_btn", "Left", isActive: settings.TextAlign == TaskTextAlign.Left)) settings.TextAlign = TaskTextAlign.Left;
        if (UI.Button("align_center_btn", "Center", isActive: settings.TextAlign == TaskTextAlign.Center)) settings.TextAlign = TaskTextAlign.Center;
        UI.EndHBoxContainer();

        var closeButtonPos = new Vector2((windowSize.X - 100) / 2, windowSize.Y - 60);
        if (UI.Button("close_settings_btn", "Close", size: new Vector2(100, 40), origin: closeButtonPos))
        {
            _windowHost.ModalWindowService.CloseModalWindow(0);
        }
        UI.EndVBoxContainer();
    }

    public void OpenAddTaskModal(KanbanColumn column)
    {
        if (IsModalOpen) return;
        _columnToAddTaskTo = column;
        _taskText = "";
        _selectedColorHex = _availableTaskColors[0];
        _windowHost.ModalWindowService.OpenModalWindow("Create New Task", 450, 280, DrawAddTaskModalUI, resultCode =>
        {
            if (resultCode == 0 && !string.IsNullOrWhiteSpace(_taskText))
            {
                var newTask = new KanbanTask { Text = _taskText.Trim(), ColorHex = _selectedColorHex };
                _columnToAddTaskTo.Tasks.Add(newTask);
                RequestSave();
            }
            _columnToAddTaskTo = null;
        });
    }

    private void DrawAddTaskModalUI(UIContext context)
    {
        var windowSize = context.Renderer.RenderTargetSize;
        UI.BeginVBoxContainer("add_task_vbox", new Vector2(20, 20), gap: 15f);
        UI.Text("add_task_title", "Create New Task", style: new ButtonStyle { FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });
        UI.InputText("new_task_input", ref _taskText, new Vector2(windowSize.X - 40, 40), placeholderText: "Enter task description...");

        DrawColorSelector("add_task", ref _selectedColorHex);
        UI.Separator(windowSize.X - 40);

        UI.BeginHBoxContainer("add_task_actions_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("save_task_btn", "Save Task", size: new Vector2(120, 40))) _windowHost.ModalWindowService.CloseModalWindow(0);
        if (UI.Button("cancel_task_btn", "Cancel", size: new Vector2(120, 40))) _windowHost.ModalWindowService.CloseModalWindow(1);
        UI.EndHBoxContainer();
        UI.EndVBoxContainer();
    }

    public void OpenEditTaskModal(KanbanTask task)
    {
        if (IsModalOpen) return;
        _taskToEdit = task;
        _taskText = task.Text;
        _selectedColorHex = task.ColorHex;
        _windowHost.ModalWindowService.OpenModalWindow("Edit Task", 450, 320, DrawEditTaskModalUI, resultCode =>
        {
            if (resultCode == 0 && !string.IsNullOrWhiteSpace(_taskText) && _taskToEdit != null)
            {
                _taskToEdit.Text = _taskText.Trim();
                _taskToEdit.ColorHex = _selectedColorHex;
                RequestSave();
            }
            else if (resultCode == 2 && _taskToEdit != null)
            {
                var column = _board.Columns.FirstOrDefault(c => c.Tasks.Contains(_taskToEdit));
                column?.Tasks.Remove(_taskToEdit);
                RequestSave();
            }
            _taskToEdit = null;
        });
    }

    private void DrawEditTaskModalUI(UIContext context)
    {
        var windowSize = context.Renderer.RenderTargetSize;
        UI.BeginVBoxContainer("edit_task_vbox", new Vector2(20, 20), gap: 15f);
        UI.Text("edit_task_title", "Edit Task", style: new ButtonStyle { FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });
        UI.InputText("edit_task_input", ref _taskText, new Vector2(windowSize.X - 40, 40), placeholderText: "Enter task description...");

        DrawColorSelector("edit_task", ref _selectedColorHex);
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