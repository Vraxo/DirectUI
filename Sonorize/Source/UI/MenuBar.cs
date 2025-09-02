// Sonorize/Source/UI/MenuBar.cs
using System;
using System.Numerics;
using DirectUI;
using DirectUI.Core;

namespace Sonorize;

public class MenuBar
{
    private readonly Action _onOpenSettings;
    private bool _isFileMenuOpen = false;
    private readonly int _fileMenuPopupId;

    public MenuBar(Action onOpenSettings)
    {
        _onOpenSettings = onOpenSettings ?? throw new ArgumentNullException(nameof(onOpenSettings));
        _fileMenuPopupId = "fileMenuPopup".GetHashCode();
    }

    public void Draw(UIContext context)
    {
        var menuBarBg = new BoxStyle
        {
            FillColor = new(0.1f, 0.1f, 0.1f, 1.0f),
            BorderColor = new(0.05f, 0.05f, 0.05f, 1.0f),
            BorderLengthBottom = 1f,
            BorderLengthTop = 0,
            BorderLengthLeft = 0,
            BorderLengthRight = 0,
            Roundness = 0
        };
        context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, 0, context.Renderer.RenderTargetSize.X, 30), menuBarBg);

        UI.BeginHBoxContainer("menuBar", new Vector2(5, 4), 5);

        var fileButtonPos = context.Layout.GetCurrentPosition();
        var fileButtonSize = new Vector2(50, 22);

        if (_isFileMenuOpen && UI.State.ActivePopupId != _fileMenuPopupId)
        {
            _isFileMenuOpen = false;
        }

        if (UI.Button("fileMenu", "File", fileButtonSize, isActive: _isFileMenuOpen, layer: 20))
        {
            _isFileMenuOpen = !_isFileMenuOpen;
            if (_isFileMenuOpen)
            {
                OpenMenuPopup(fileButtonPos, fileButtonSize);
            }
            else
            {
                UI.State.ClearActivePopup();
            }
        }
        UI.EndHBoxContainer();
    }

    private void OpenMenuPopup(Vector2 buttonPos, Vector2 buttonSize)
    {
        var popupPosition = new Vector2(buttonPos.X, buttonPos.Y + buttonSize.Y + 2);
        var popupSize = new Vector2(150, 30);
        var popupBounds = new Vortice.Mathematics.Rect(popupPosition.X, popupPosition.Y, popupSize.X, popupSize.Y);

        Action<UIContext> drawCallback = (ctx) =>
        {
            var popupStyle = new BoxStyle { FillColor = DefaultTheme.NormalFill, BorderColor = DefaultTheme.FocusBorder, BorderLength = 1f, Roundness = 0.1f };
            ctx.Renderer.DrawBox(popupBounds, popupStyle);

            var itemTheme = new ButtonStylePack { Roundness = 0f, BorderLength = 0f };
            itemTheme.Normal.FillColor = DefaultTheme.Transparent;
            itemTheme.Hover.FillColor = DefaultTheme.HoverFill;
            itemTheme.Pressed.FillColor = DefaultTheme.Accent;

            UI.BeginVBoxContainer("popupContent", popupBounds.TopLeft);
            if (UI.Button("settingsBtn", "Settings", new Vector2(popupBounds.Width, popupBounds.Height), itemTheme, textAlignment: new Alignment(HAlignment.Left, VAlignment.Center), textMargin: new Vector2(5, 0), layer: 50))
            {
                UI.State.ClearActivePopup();
                _isFileMenuOpen = false;
                _onOpenSettings();
            }
            UI.EndVBoxContainer();
        };
        UI.State.SetActivePopup(_fileMenuPopupId, drawCallback, popupBounds);
    }
}