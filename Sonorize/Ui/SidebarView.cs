using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;
using Sonorize.Services;
using System.Numerics;

namespace Sonorize;

public class SidebarView
{
    private readonly Settings _settings;
    private readonly UIStateService _uiState;
    public Action? OnCreatePlaylist { get; set; }

    public const float SidebarWidthExpanded = 230f;
    public const float SidebarWidthCollapsed = 68f;
    private float SidebarWidth => _uiState.IsSidebarCollapsed ? SidebarWidthCollapsed : SidebarWidthExpanded;

    public SidebarView(
        Settings settings,
        UIStateService uiState,
        Action? onCreatePlaylist)
    {
        _settings = settings;
        _uiState = uiState;
        OnCreatePlaylist = onCreatePlaylist;
    }

    public void Draw(UIContext context, float availableHeight)
    {
        float innerWidth = SidebarWidth - 20;
        if (innerWidth < 0) innerWidth = 0;

        DrawBackground(context, availableHeight);

        UI.BeginVBoxContainer("sidebarContent", new Vector2(10, 20), 5, new Vector2(innerWidth, 0));
        {
            DrawHeader(context, innerWidth);
            DrawLibraryNavigation(context, innerWidth);
            DrawPlaylistsHeader(context, innerWidth);
            DrawPlaylistList(context, availableHeight, innerWidth);
        }
        UI.EndVBoxContainer();

        DrawFooter(context, availableHeight);
    }

    private void DrawBackground(UIContext context, float availableHeight)
    {
        var backgroundStyle = new BoxStyle
        {
            FillColor = new(0, 0, 0, 255),
            Roundness = 0,
            BorderLength = 0
        };
        var sidebarBounds = new Vortice.Mathematics.Rect(0, 0, SidebarWidth, availableHeight);
        context.Renderer.DrawBox(sidebarBounds, backgroundStyle);
    }

    private void DrawHeader(UIContext context, float innerWidth)
    {
        if (_uiState.IsSidebarCollapsed)
        {
            var logoStyle = new ButtonStyle { FontSize = 24, FontWeight = Vortice.DirectWrite.FontWeight.Bold };
            UI.Text("logo", "S", new Vector2(innerWidth, 24), style: logoStyle, textAlignment: new Alignment(HAlignment.Center, VAlignment.Center));
        }
        else
        {
            UI.Text("logo", "Sonorize", style: new ButtonStyle { FontSize = 24 });
        }
        UI.Separator(innerWidth, 1f, 10f);
    }

    private void DrawLibraryNavigation(UIContext context, float innerWidth)
    {
        var theme = CreateNavigationButtonTheme();
        var text = _uiState.IsSidebarCollapsed ? "\uF02D" : "Your Library"; // fa-book
        var alignment = _uiState.IsSidebarCollapsed ? new Alignment(HAlignment.Center, VAlignment.Center) : new Alignment(HAlignment.Left, VAlignment.Center);
        var textMargin = _uiState.IsSidebarCollapsed ? Vector2.Zero : new Vector2(10, 0);

        if (UI.Button("navLibrary", text, new Vector2(innerWidth, 30),
            theme: theme, isActive: _uiState.ActiveSidebarIndex == 0,
            textAlignment: alignment, textMargin: textMargin))
        {
            _uiState.ActiveSidebarIndex = 0;
        }
    }

    private void DrawPlaylistsHeader(UIContext context, float innerWidth)
    {
        UI.Separator(innerWidth, 1f, 10f);
        UI.BeginHBoxContainer("playlistHeader", UI.Context.Layout.GetCurrentPosition(), 5, VAlignment.Center, 20);
        {
            if (!_uiState.IsSidebarCollapsed)
            {
                var textStyle = new ButtonStyle { FontSize = 12, FontColor = new(0.7f, 0.7f, 0.7f, 1f) };
                var textSize = context.TextService.MeasureText("PLAYLISTS", textStyle);
                UI.Text("playlistsLabel", "PLAYLISTS", style: textStyle);

                var addButtonSize = new Vector2(20, 20);
                float spacerWidth = innerWidth - textSize.X - 5 - addButtonSize.X;
                if (spacerWidth > 0)
                {
                    UI.Context.Layout.AdvanceLayout(new Vector2(spacerWidth, 0));
                }
            }

            var addPlaylistButtonSize = _uiState.IsSidebarCollapsed ? new Vector2(innerWidth, 20) : new Vector2(20, 20);
            if (UI.Button("addPlaylist", "+", addPlaylistButtonSize, layer: 10))
            {
                OnCreatePlaylist?.Invoke();
            }
        }
        UI.EndHBoxContainer();
    }

    private void DrawPlaylistList(UIContext context, float availableHeight, float innerWidth)
    {
        float reservedFooterHeight = 50;
        float scrollHeight = availableHeight - UI.Context.Layout.GetCurrentPosition().Y - reservedFooterHeight;
        if (scrollHeight < 0) scrollHeight = 0;

        UI.BeginScrollableRegion("playlistScroll", new Vector2(innerWidth, scrollHeight), gap: 2);
        {
            float scrollableContentWidth = UI.GetAvailableContentWidth();
            var theme = CreateNavigationButtonTheme();

            for (int i = 0; i < _settings.Playlists.Count; i++)
            {
                DrawPlaylistItem(i, scrollableContentWidth, theme);
            }
        }
        UI.EndScrollableRegion();
    }

    private void DrawPlaylistItem(int index, float availableWidth, ButtonStylePack theme)
    {
        var playlist = _settings.Playlists[index];
        bool isCollapsed = _uiState.IsSidebarCollapsed;

        var icon = "\uF001"; // fa-music
        var text = isCollapsed ? icon : playlist.Name;
        var alignment = isCollapsed ? new Alignment(HAlignment.Center, VAlignment.Center) : new Alignment(HAlignment.Left, VAlignment.Center);
        var textMargin = isCollapsed ? Vector2.Zero : new Vector2(10, 0);

        if (UI.Button($"navPlaylist_{index}", text, new Vector2(availableWidth, 30),
            theme: theme, isActive: _uiState.ActiveSidebarIndex == index + 1,
            textAlignment: alignment, textMargin: textMargin))
        {
            _uiState.ActiveSidebarIndex = index + 1;
        }
    }

    private void DrawFooter(UIContext context, float availableHeight)
    {
        bool isCollapsed = _uiState.IsSidebarCollapsed;
        var buttonSize = new Vector2(32, 32);
        var buttonPosition = new Vector2((SidebarWidth - buttonSize.X) / 2, availableHeight - buttonSize.Y - 10);
        var icon = isCollapsed ? "\uF054" : "\uF053"; // right chevron : left chevron

        var theme = new ButtonStylePack { FontName = "FontAwesomeSolid", FontSize = 14, Roundness = 1f };
        theme.Normal.FillColor = Colors.Transparent;
        theme.Hover.FillColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        if (UI.Button("collapseSidebar", icon, buttonSize, theme: theme, origin: buttonPosition))
        {
            _uiState.IsSidebarCollapsed = !isCollapsed;
        }
    }

    private ButtonStylePack CreateNavigationButtonTheme()
    {
        var theme = new ButtonStylePack { Roundness = 0.1f };
        theme.Normal.FillColor = Colors.Transparent;
        theme.Active.FillColor = new(0.15f, 0.15f, 0.15f, 1f);
        theme.Hover.FillColor = new(0.1f, 0.1f, 0.1f, 1f);
        theme.ActiveHover.FillColor = new(0.15f, 0.15f, 0.15f, 1f);
        theme.FontName = _uiState.IsSidebarCollapsed ? "FontAwesomeSolid" : "Segoe UI";
        return theme;
    }
}