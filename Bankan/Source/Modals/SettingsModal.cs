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
        var windowSize = context.Renderer.RenderTargetSize;
        UI.BeginVBoxContainer("settings_vbox", new Vector2(20, 20), gap: 15f);
        UI.Text("settings_title", "Settings", style: new ButtonStyle { FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });
        UI.Separator(windowSize.X - 40);

        UI.Text("color_style_label", "Task Color Style");
        UI.BeginHBoxContainer("color_style_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("style_border_btn", "Border", isActive: _settings.ColorStyle == TaskColorStyle.Border)) _settings.ColorStyle = TaskColorStyle.Border;
        if (UI.Button("style_bg_btn", "Background", isActive: _settings.ColorStyle == TaskColorStyle.Background)) _settings.ColorStyle = TaskColorStyle.Background;
        UI.EndHBoxContainer();

        UI.Text("align_label", "Task Text Alignment");
        UI.BeginHBoxContainer("align_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("align_left_btn", "Left", isActive: _settings.TextAlign == TaskTextAlign.Left)) _settings.TextAlign = TaskTextAlign.Left;
        if (UI.Button("align_center_btn", "Center", isActive: _settings.TextAlign == TaskTextAlign.Center)) _settings.TextAlign = TaskTextAlign.Center;
        UI.EndHBoxContainer();

        var closeButtonPos = new Vector2((windowSize.X - 100) / 2, windowSize.Y - 60);
        if (UI.Button("close_settings_btn", "Close", size: new Vector2(100, 40), origin: closeButtonPos))
        {
            _windowHost.ModalWindowService.CloseModalWindow(0);
        }
        UI.EndVBoxContainer();
    }
}