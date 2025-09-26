using Bankan.Modals;
using DirectUI.Core;

namespace Bankan;

public class ModalManager
{
    public bool IsModalOpen => _windowHost.ModalWindowService.IsModalWindowOpen;

    private readonly IWindowHost _windowHost;
    private readonly KanbanBoard _board;
    private readonly Action _saveRequestCallback;
    
    private readonly AddTaskModal _addTaskModal;
    private readonly EditTaskModal _editTaskModal;

    private Task? _taskToDelete;

    public ModalManager(IWindowHost windowHost, KanbanBoard board, Action saveRequestCallback)
    {
        _windowHost = windowHost;
        _board = board;
        _saveRequestCallback = saveRequestCallback;

        List<string> availableColors = ["#bb86fc", "#ff7597", "#75ffff", "#75ff9f", "#ffdf75"];
        _addTaskModal = new(_windowHost, availableColors);
        _editTaskModal = new(_windowHost, availableColors);
    }

    public void RequestSave()
    {
        _saveRequestCallback.Invoke();
    }

    public void RequestTaskDeletion(Task task)
    {
        _taskToDelete = task;
    }

    public void ProcessPendingActions()
    {
        if (_taskToDelete is null)
        {
            return;
        }
        
        KanbanColumn? column = _board.Columns.FirstOrDefault(c => c.Tasks.Contains(_taskToDelete));
        
        if (column is not null)
        {
            column.Tasks.Remove(_taskToDelete);
            RequestSave();
        }

        _taskToDelete = null;
    }

    public void OpenSettingsModal(KanbanSettings settings)
    {
        if (IsModalOpen)
        {
            return;
        }

        SettingsModal settingsModal = new(settings, _windowHost);
        
        _windowHost.ModalWindowService.OpenModalWindow(
            "Settings", 
            400, 
            250,
            settingsModal.DrawUI,
            _ => RequestSave()
        );
    }

    public void OpenAddTaskModal(KanbanColumn column)
    {
        if (IsModalOpen)
        {
            return;
        }

        _addTaskModal.Open();

        _windowHost.ModalWindowService.OpenModalWindow(
            "Create New Task", 
            450, 
            280,
            _addTaskModal.DrawUI,
            resultCode =>
            {
                AddTask(column, resultCode);
            }
        );
    }

    private void AddTask(KanbanColumn column, int resultCode)
    {
        if (resultCode != 0 || string.IsNullOrWhiteSpace(_addTaskModal.TaskText))
        {
            return;
        }

        Task newTask = new()
        {
            Text = _addTaskModal.TaskText.Trim(),
            ColorHex = _addTaskModal.SelectedColorHex
        };

        column.Tasks.Add(newTask);
        RequestSave();
    }

    public void OpenEditTaskModal(Task task)
    {
        if (IsModalOpen)
        {
            return;
        }

        _editTaskModal.Open(task);
        
        _windowHost.ModalWindowService.OpenModalWindow(
            "Edit Task", 
            450, 
            320,
            _editTaskModal.DrawUI,
            EditTask
        );
    }

    private void EditTask(int resultCode)
    {
        Task? taskToEdit = _editTaskModal.TaskToEdit;

        if (taskToEdit is null)
        {
            return;
        }

        switch (resultCode)
        {
            case 0 when !string.IsNullOrWhiteSpace(_editTaskModal.TaskText):
                taskToEdit.Text = _editTaskModal.TaskText.Trim();
                taskToEdit.ColorHex = _editTaskModal.SelectedColorHex;
                RequestSave();
                return;
            case 2:
                RequestTaskDeletion(taskToEdit);
                break;
        }
    }
}