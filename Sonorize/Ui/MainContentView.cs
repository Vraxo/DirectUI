using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;
using Sonorize.Services;
using System.Numerics;

namespace Sonorize;

public class MainContentView
{
    private readonly LibraryView _libraryView;
    private readonly Settings _settings;
    private readonly UIStateService _uiState;
    public Action? OpenSettingsCallback { get; set; }

    private float SidebarWidth => _uiState.IsSidebarCollapsed ? SidebarView.SidebarWidthCollapsed : SidebarView.SidebarWidthExpanded;

    public MainContentView(
        LibraryView libraryView,
        Settings settings,
        UIStateService uiState,
        Action? openSettingsCallback)
    {
        _libraryView = libraryView;
        _settings = settings;
        _uiState = uiState;
        OpenSettingsCallback = openSettingsCallback;
    }

    public void Draw(UIContext context, Vector2 mainAreaSize)
    {
        var contentWidth = mainAreaSize.X - SidebarWidth;
        var contentHeight = mainAreaSize.Y;
        var contentPosition = new Vector2(SidebarWidth, 0);

        DrawBackground(context, contentPosition, contentWidth, contentHeight);

        float headerHeight = DrawHeader(context, contentPosition, contentWidth);

        DrawContentArea(context, contentPosition, contentWidth, contentHeight, headerHeight);
    }

    private void DrawBackground(UIContext context, Vector2 position, float width, float height)
    {
        var backgroundStyle = new BoxStyle { FillColor = new(18 / 255f, 18 / 255f, 18 / 255f, 1.0f), Roundness = 0, BorderLength = 0 };
        var bounds = new Vortice.Mathematics.Rect(position.X, position.Y, width, height);
        context.Renderer.DrawBox(bounds, backgroundStyle);
    }

    private float DrawHeader(UIContext context, Vector2 contentPosition, float contentWidth)
    {
        float headerHeight = 40f;
        var headerPosition = contentPosition + new Vector2(20, 10);
        var headerWidth = contentWidth - 40;

        UI.BeginHBoxContainer("mainHeader", headerPosition, 5, VAlignment.Center, headerHeight);
        {
            DrawHeaderTitle(context);
            DrawHeaderSpacer(context, headerWidth);
            DrawSettingsButton();
        }
        UI.EndHBoxContainer();

        return headerHeight;
    }

    private void DrawHeaderTitle(UIContext context)
    {
        string title = "Your Library";
        if (_uiState.ActiveSidebarIndex > 0 && _uiState.ActiveSidebarIndex - 1 < _settings.Playlists.Count)
        {
            title = _settings.Playlists[_uiState.ActiveSidebarIndex - 1].Name;
        }
        UI.Text("mainContentTitle", title, style: new ButtonStyle { FontSize = 24 });
    }

    private void DrawHeaderSpacer(UIContext context, float headerWidth)
    {
        string title = "Your Library";
        if (_uiState.ActiveSidebarIndex > 0 && _uiState.ActiveSidebarIndex - 1 < _settings.Playlists.Count)
        {
            title = _settings.Playlists[_uiState.ActiveSidebarIndex - 1].Name;
        }
        var titleStyle = new ButtonStyle { FontSize = 24 };
        var titleSize = context.TextService.MeasureText(title, titleStyle);
        var settingsButtonSize = new Vector2(30, 30);
        float spacerWidth = headerWidth - titleSize.X - settingsButtonSize.X;
        if (spacerWidth > 0)
        {
            UI.Context.Layout.AdvanceLayout(new Vector2(spacerWidth, 0));
        }
    }

    private void DrawSettingsButton()
    {
        var theme = new ButtonStylePack { FontSize = 16, FontName = "Segoe UI Emoji", Roundness = 1f };
        theme.Normal.FillColor = Colors.Transparent;
        theme.Hover.FillColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        if (UI.Button("settingsButton", "⚙️", new Vector2(30, 30), theme: theme, layer: 10))
        {
            OpenSettingsCallback?.Invoke();
        }
    }

    private void DrawContentArea(UIContext context, Vector2 contentPosition, float contentWidth, float contentHeight, float headerHeight)
    {
        var gridPosition = new Vector2(contentPosition.X + 10, contentPosition.Y + 10 + headerHeight);
        var gridSize = new Vector2(
            contentWidth - 20,
            contentHeight - headerHeight - 20
        );

        _libraryView.Draw(context, gridPosition, gridSize, _uiState.ActiveSidebarIndex, _settings, _uiState.SearchText);
    }
}