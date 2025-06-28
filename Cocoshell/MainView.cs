using System;
using System.IO;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

/// <summary>
/// Encapsulates the state and drawing logic for the main application view.
/// </summary>
public class MainView
{
    // State for the main view's widgets
    private float _sliderValue = 0.5f;
    private float _leftPanelWidth = 250f;
    private float _rightPanelWidth = 250f;
    private float _bottomPanelHeight = 150f;
    private readonly TreeNode<string> _fileRoot;
    private readonly TreeStyle _treeStyle = new();

    public MainView()
    {
        // Load scene data for the tree view
        try
        {
            string scenePath = @"D:\Parsa Stuff\Visual Studio\Cosmocrush\Cosmocrush\Res\Scenes\Player.yaml";
            _fileRoot = File.Exists(scenePath)
                ? SceneParser.Parse(scenePath)
                : CreateDefaultTree("Scene file not found", scenePath);
        }
        catch (Exception ex)
        {
            _fileRoot = CreateDefaultTree($"Error parsing scene: {ex.Message}", "");
        }
    }

    /// <summary>
    /// The main drawing entry point for this view.
    /// </summary>
    public void Draw(UIContext context, Action openProjectWindowAction)
    {
        DrawMenuBar(context, openProjectWindowAction);
        DrawMainLayoutPanels(context);
    }

    private void DrawMenuBar(UIContext context, Action openProjectWindowAction)
    {
        float menuBarHeight = 30f;
        var rt = context.RenderTarget;

        // Draw menu bar background
        var menuBarBackgroundBrush = UI.Resources.GetOrCreateBrush(rt, new Color4(37 / 255f, 37 / 255f, 38 / 255f, 1f));
        var menuBarBorderBrush = UI.Resources.GetOrCreateBrush(rt, DefaultTheme.NormalBorder);
        rt.FillRectangle(new Rect(0, 0, rt.Size.Width, menuBarHeight), menuBarBackgroundBrush);
        rt.DrawLine(new Vector2(0, menuBarHeight - 1), new Vector2(rt.Size.Width, menuBarHeight - 1), menuBarBorderBrush, 1f);

        // Define a shared style for all menu buttons using the style stack
        UI.PushStyleVar(StyleVar.FrameRounding, 0.0f);
        UI.PushStyleVar(StyleVar.FrameBorderSize, 0.0f);
        UI.PushStyleColor(StyleColor.Button, Colors.Transparent);
        UI.PushStyleColor(StyleColor.ButtonHovered, new Color4(63 / 255f, 63 / 255f, 70 / 255f, 1f));
        UI.PushStyleColor(StyleColor.ButtonPressed, DefaultTheme.Accent);
        UI.PushStyleColor(StyleColor.Text, new Color4(204 / 255f, 204 / 255f, 204 / 255f, 1f));

        UI.BeginHBoxContainer("menu_bar", new Vector2(5, 0), 0);
        if (MenuBarButton("file_button", "File")) { /* File logic */ }
        if (MenuBarButton("project_button", "Project"))
        {
            openProjectWindowAction?.Invoke();
        }
        if (MenuBarButton("edit_button", "Edit")) { /* Edit logic */ }
        if (MenuBarButton("view_button", "View")) { /* View logic */ }
        if (MenuBarButton("help_button", "Help")) { /* Help logic */ }
        UI.EndHBoxContainer();

        UI.PopStyleColor(4); // Pop all 4 colors
        UI.PopStyleVar(2);   // Pop both style vars
    }

    private void DrawMainLayoutPanels(UIContext context)
    {
        float menuBarHeight = 30f;
        var panelStyle = new BoxStyle { BorderLength = 1, Roundness = 0f };

        // --- Left Panel (Scene Tree) ---
        UI.BeginResizableVPanel("left_panel", ref _leftPanelWidth, HAlignment.Left, menuBarHeight,
            minWidth: 150, maxWidth: 400, padding: new Vector2(10, 10), gap: 10, panelStyle: panelStyle);
        {
            UI.BeginVBoxContainer("tree_vbox", UI.Context.Layout.GetCurrentPosition(), 0);
            UI.Tree("file_tree", _fileRoot, out var clickedNode, _treeStyle);
            if (clickedNode is not null) Console.WriteLine($"Tree Node Clicked: '{clickedNode.Text}', Path: {clickedNode.UserData}");
            UI.EndVBoxContainer();
        }
        UI.EndResizableVPanel();

        // --- Right Panel (Properties) ---
        UI.BeginResizableVPanel("right_panel", ref _rightPanelWidth, HAlignment.Right, menuBarHeight,
            minWidth: 150, maxWidth: 400, padding: new Vector2(10, 10), gap: 10, panelStyle: panelStyle);
        {
            UI.PushStyleVar(StyleVar.FrameRounding, 0.2f); // Rounded buttons in this panel
            if (UI.Button("right_button_1", "Right Panel")) { /* ... */ }
            if (UI.Button("right_button_2", "Another Button")) { /* ... */ }
            UI.PopStyleVar();

            _sliderValue = UI.HSlider("my_slider", _sliderValue, 0f, 1f, new Vector2(200, 20));
        }
        UI.EndResizableVPanel();

        // --- Bottom Panel ---
        UI.BeginResizableHPanel("bottom_panel", ref _bottomPanelHeight, _leftPanelWidth, _rightPanelWidth, menuBarHeight,
            minHeight: 50, maxHeight: 300, padding: new Vector2(10, 10), gap: 10, panelStyle: panelStyle);
        {
            UI.PushStyleVar(StyleVar.FrameRounding, 0f); // Sharp buttons in this panel
            if (UI.Button("bottom_button", "Bottom Panel Button")) { /* ... */ }
            UI.PopStyleVar();
        }
        UI.EndResizableHPanel();
    }

    private bool MenuBarButton(string id, string text)
    {
        var menuButtonSize = new Vector2(0, 30); // Auto-width, height of menu bar
        var menuButtonAlignment = new Alignment(HAlignment.Center, VAlignment.Center);
        var menuButtonTextMargin = new Vector2(10, 0);

        return UI.Button(id, text, menuButtonSize, autoWidth: true, textMargin: menuButtonTextMargin, textAlignment: menuButtonAlignment);
    }

    private TreeNode<string> CreateDefaultTree(string reason, string path)
    {
        var root = new TreeNode<string>("Error", "Could not load scene", true);
        root.AddChild(reason, "");
        if (!string.IsNullOrEmpty(path))
        {
            root.AddChild($"Path: {path}", "");
        }
        return root;
    }
}