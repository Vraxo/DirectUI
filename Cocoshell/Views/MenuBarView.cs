using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public class MenuBarView
{
    private const float MenuBarHeight = 30f;

    public void Draw(UIContext context, Action openProjectWindowAction)
    {
        var renderer = context.Renderer;

        // Use renderer's GetOrCreateBrush and DrawBox/DrawLine methods
        var menuBarBackgroundBrushColor = new Color4(37 / 255f, 37 / 255f, 38 / 255f, 1f);
        var menuBarBorderColor = DefaultTheme.NormalBorder;

        renderer.DrawBox(new Rect(0, 0, renderer.RenderTargetSize.X, MenuBarHeight), new BoxStyle { FillColor = menuBarBackgroundBrushColor, Roundness = 0f, BorderLength = 0f });
        renderer.DrawLine(new Vector2(0, MenuBarHeight - 1), new Vector2(renderer.RenderTargetSize.X, MenuBarHeight - 1), menuBarBorderColor, 1f);

        // Define a shared style for all menu buttons
        UI.PushStyleVar(StyleVar.FrameRounding, 0.0f);
        UI.PushStyleVar(StyleVar.FrameBorderSize, 0.0f);
        UI.PushStyleColor(StyleColor.Button, Colors.Transparent);
        UI.PushStyleColor(StyleColor.ButtonHovered, new Color4(63 / 255f, 63 / 255f, 70 / 255f, 1f));
        UI.PushStyleColor(StyleColor.ButtonPressed, DefaultTheme.Accent);
        UI.PushStyleColor(StyleColor.Text, new Color4(204 / 255f, 204 / 255f, 204 / 255f, 1f));

        UI.BeginHBoxContainer("menu_bar", new Vector2(5, 0), 0);
        {
            if (MenuBarButton("file_button", "File"))
            {

            }

            if (MenuBarButton("project_button", "Project"))
            {
                openProjectWindowAction?.Invoke();
            }

            if (MenuBarButton("edit_button", "Edit"))
            {

            }

            if (MenuBarButton("view_button", "View"))
            {

            }

            if (MenuBarButton("help_button", "Help"))
            {

            }
        }
        UI.EndHBoxContainer();

        UI.PopStyleColor(4);
        UI.PopStyleVar(2);
    }

    private static bool MenuBarButton(string id, string text)
    {
        return UI.Button(
            id,
            text,
            size: new(0, MenuBarHeight),
            autoWidth: true,
            textMargin: new(10, 0),
            textAlignment: new(HAlignment.Center, VAlignment.Center));
    }
}