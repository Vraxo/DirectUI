using System.Numerics;
using DirectUI;
using DirectUI.Core;

namespace Bankan.Modals;

public class SettingsModal
{
    private readonly KanbanSettings _settings;
    private readonly IWindowHost _windowHost;

    public SettingsModal(KanbanSettings settings, IWindowHost windowHost)
    {
        _settings = settings;
        _windowHost = windowHost;
    }

    public void DrawUI(UIContext context)
    {
        Vector2 windowSize = context.Renderer.RenderTargetSize;

        UI.BeginVBoxContainer("settings_vbox", new(20, 20), gap: 15f);
        {
            DrawHeader(windowSize);
            DrawColorStyleSelectors();
            DrawTextAlignSelectors();
            DrawCloseButton(windowSize);
        }
        UI.EndVBoxContainer();
    }

    private static void DrawHeader(Vector2 windowSize)
    {
        ButtonStyle style = new()
        {
            FontSize = 18,
            FontWeight =
            Vortice.DirectWrite.FontWeight.SemiBold
        };

        UI.Text("settings_title", "Settings", style: style);

        UI.Separator(windowSize.X - 40);
    }

    private void DrawColorStyleSelectors()
    {
        UI.Text("color_style_label", "Task Color Style");

        int colorStyleIndex = (int)_settings.ColorStyle;

        if (UI.RadioButtons("color_style_selectors", ["Border", "Background"], ref colorStyleIndex))
        {
            _settings.ColorStyle = (TaskColorStyle)colorStyleIndex;
        }
    }

    private void DrawTextAlignSelectors()
    {
        UI.Text("align_label", "Task Text Alignment");

        int textAlignIndex = (int)_settings.TextAlign;

        if (UI.RadioButtons("text_align_selectors", ["Left", "Center"], ref textAlignIndex))
        {
            _settings.TextAlign = (TaskTextAlign)textAlignIndex;
        }
    }

    private void DrawCloseButton(Vector2 windowSize)
    {
        Vector2 closeButtonPos = new((windowSize.X - 100) / 2, windowSize.Y - 60);

        if (UI.Button("close_settings_btn", "Close", size: new(100, 40), origin: closeButtonPos))
        {
            _windowHost.ModalWindowService.CloseModalWindow(0);
        }
    }
}