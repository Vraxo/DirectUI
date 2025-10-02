using DirectUI;
using DirectUI.Core;
using System.Numerics;
using Tagra.Data;

namespace Tagra;

public static class ModalDialogs
{
    public static void DrawDeleteConfirmation(IWindowHost host, Tag tagToDelete)
    {
        UI.BeginVBoxContainer("delete_modal_vbox", new Vector2(10, 10), gap: 15f);

        UI.WrappedText("delete_confirm_text", $"Are you sure you want to delete the tag '{tagToDelete.Name}'? This will remove it from {tagToDelete.FileCount} file(s) and cannot be undone.", new Vector2(280, 0));

        UI.BeginHBoxContainer("delete_modal_buttons", UI.Context.Layout.GetCurrentPosition(), gap: 10f);

        if (UI.Button("delete_yes", "Yes, Delete", new Vector2(120, 32)))
        {
            host.ModalWindowService.CloseModalWindow(0); // 0 for 'Yes'
        }
        if (UI.Button("delete_no", "Cancel", new Vector2(100, 32)))
        {
            host.ModalWindowService.CloseModalWindow(1); // 1 for 'Cancel'
        }

        UI.EndHBoxContainer();
        UI.EndVBoxContainer();
    }
}