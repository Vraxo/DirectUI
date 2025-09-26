using System;
using System.Collections.Generic;
using System.Linq;
using Bankan.Modals;
using DirectUI;
using DirectUI.Core;

namespace Bankan;

public class ModalManager
{
    private readonly IWindowHost _windowHost;
    private readonly KanbanBoard _board;
    private readonly Action _saveRequestCallback;

    private readonly AddTaskModal _addTaskModal;
    private readonly EditTaskModal _editTaskModal;

    private object? _activeModalLogic;

    private Task? _taskToDelete;

    public bool IsModalOpen => _windowHost.ModalWindowService.IsModalWindowOpen;

    public ModalManager(IWindowHost windowHost, KanbanBoard board, Action saveRequestCallback)
    {
        _windowHost = windowHost;
        _board = board;
        _saveRequestCallback = saveRequestCallback;

        var availableColors = new List<string> { "#bb86fc", "#ff7597", "#75ffff", "#75ff9f", "#ffdf75" };
        _addTaskModal = new AddTaskModal(_windowHost, availableColors);
        _editTaskModal = new EditTaskModal(_windowHost, availableColors);
    }

    public void RequestSave() => _saveRequestCallback.Invoke();

    public void RequestTaskDeletion(Task task) => _taskToDelete = task;

    public void ProcessPendingActions()
    {
        if (_taskToDelete != null)
        {
            var column = _board.Columns.FirstOrDefault(c => c.Tasks.Contains(_taskToDelete));
            if (column != null)
            {
                column.Tasks.Remove(_taskToDelete);
                RequestSave();
            }
            _taskToDelete = null;
        }
    }

    public void OpenSettingsModal(KanbanSettings settings)
    {
        if (IsModalOpen) return;
        var settingsModal = new SettingsModal(settings, _windowHost);
        _activeModalLogic = settingsModal;
        _windowHost.ModalWindowService.OpenModalWindow(
            "Settings", 400, 250,
            settingsModal.DrawUI,
            _ => RequestSave()
        );
    }

    public void OpenAddTaskModal(KanbanColumn column)
    {
        if (IsModalOpen) return;
        _addTaskModal.Open();
        _activeModalLogic = _addTaskModal;
        _windowHost.ModalWindowService.OpenModalWindow(
            "Create New Task", 450, 280,
            _addTaskModal.DrawUI,
            resultCode =>
            {
                if (resultCode == 0 && !string.IsNullOrWhiteSpace(_addTaskModal.TaskText))
                {
                    var newTask = new Task { Text = _addTaskModal.TaskText.Trim(), ColorHex = _addTaskModal.SelectedColorHex };
                    column.Tasks.Add(newTask);
                    RequestSave();
                }
            }
        );
    }

    public void OpenEditTaskModal(Task task)
    {
        if (IsModalOpen) return;
        _editTaskModal.Open(task);
        _activeModalLogic = _editTaskModal;
        _windowHost.ModalWindowService.OpenModalWindow(
            "Edit Task", 450, 320,
            _editTaskModal.DrawUI,
            resultCode =>
            {
                var taskToEdit = _editTaskModal.TaskToEdit;
                if (taskToEdit == null) return;

                if (resultCode == 0 && !string.IsNullOrWhiteSpace(_editTaskModal.TaskText))
                {
                    taskToEdit.Text = _editTaskModal.TaskText.Trim();
                    taskToEdit.ColorHex = _editTaskModal.SelectedColorHex;
                    RequestSave();
                }
                else if (resultCode == 2)
                {
                    RequestTaskDeletion(taskToEdit);
                }
            }
        );
    }
}