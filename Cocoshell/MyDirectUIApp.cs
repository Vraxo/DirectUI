// MyDirectUIApp.cs
using System;
using System.IO;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public class MyDirectUIApp : Direct2DAppWindow
{
    // Application state remains here, where it belongs.
    private float sliderValue = 0.5f;
    private float leftPanelWidth = 250f;
    private float rightPanelWidth = 250f;
    private float bottomPanelHeight = 150f;

    // Window management state
    private ModalWindow? _projectWindow;
    private bool _isProjectWindowOpen = false;
    private bool _openProjectWindowRequested = false;

    private readonly TreeNode<string> _fileRoot;
    private readonly TreeStyle _treeStyle = new();

    public MyDirectUIApp(string title, int width, int height)
        : base(title, width, height)
    {
        // Data initialization remains here.
        try
        {
            string scenePath = @"D:\Parsa Stuff\Visual Studio\Cosmocrush\Cosmocrush\Res\Scenes\Player.yaml";
            if (File.Exists(scenePath))
            {
                _fileRoot = SceneParser.Parse(scenePath);
            }
            else
            {
                Console.WriteLine($"Warning: Scene file not found at '{scenePath}'. Loading default tree.");
                _fileRoot = CreateDefaultTree();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing scene file. Loading default tree. Error: {ex.Message}");
            _fileRoot = CreateDefaultTree();
        }
    }

    // Override FrameUpdate to handle window management outside of rendering
    public override void FrameUpdate()
    {
        ManageWindows();

        // Always call the base FrameUpdate. The AppHost.Render method is now responsible
        // for checking if the window is enabled before attempting to draw.
        base.FrameUpdate();
    }

    private void ManageWindows()
    {
        // Handle modal window creation
        if (_openProjectWindowRequested && !_isProjectWindowOpen)
        {
            _openProjectWindowRequested = false; // Consume the request
            _projectWindow = new ModalWindow(this, "Project Settings", 400, 300, DrawProjectWindowUI);
            if (_projectWindow.CreateAsModal())
            {
                _isProjectWindowOpen = true;
            }
            else
            {
                Console.WriteLine("Failed to create modal window.");
                _projectWindow?.Dispose();
                _projectWindow = null;
                _isProjectWindowOpen = false;
            }
        }

        // Handle modal window cleanup
        if (_isProjectWindowOpen && (_projectWindow == null || _projectWindow.Handle == IntPtr.Zero))
        {
            _projectWindow?.Dispose(); // Ensure cleanup
            _projectWindow = null;
            _isProjectWindowOpen = false;
        }
    }


    // The factory method implementation creates the AppHost, passing the drawing logic.
    protected override AppHost CreateAppHost()
    {
        var backgroundColor = new Color4(21 / 255f, 21 / 255f, 21 / 255f, 1.0f); // #151515
        return new AppHost(DrawUI, backgroundColor);
    }

    private TreeNode<string> CreateDefaultTree()
    {
        var root = new TreeNode<string>("Error", "Could not load scene", true);
        root.AddChild("Please check file path and format.", "");
        root.AddChild(@"Path: D:\Parsa Stuff\Visual Studio\Cosmocrush\Cosmocrush\Res\Scenes\Player.yaml", "");
        return root;
    }

    // The actual drawing logic for the main window.
    private void DrawUI(UIContext context)
    {
        // Note: UI.BeginFrame and UI.EndFrame are now called by the AppHost.
        // We just need to define the UI content for the frame.

        float menuBarHeight = 30f;

        // --- Menu Bar ---
        {
            var rt = context.RenderTarget;
            var menuBarBackgroundBrush = UI.Resources.GetOrCreateBrush(rt, new Color4(37 / 255f, 37 / 255f, 38 / 255f, 1f));
            var menuBarBorderBrush = UI.Resources.GetOrCreateBrush(rt, DefaultTheme.NormalBorder);

            if (menuBarBackgroundBrush != null)
            {
                rt.FillRectangle(new Rect(0, 0, rt.Size.Width, menuBarHeight), menuBarBackgroundBrush);
            }
            if (menuBarBorderBrush != null)
            {
                rt.DrawLine(new Vector2(0, menuBarHeight - 1), new Vector2(rt.Size.Width, menuBarHeight - 1), menuBarBorderBrush, 1f);
            }

            var menuButtonTheme = new ButtonStylePack
            {
                Roundness = 0f,
                BorderLength = 0,
                FontName = "Segoe UI",
                FontSize = 14
            };
            menuButtonTheme.Normal.FillColor = Colors.Transparent;
            menuButtonTheme.Normal.FontColor = new Color4(204 / 255f, 204 / 255f, 204 / 255f, 1f);
            menuButtonTheme.Hover.FillColor = new Color4(63 / 255f, 63 / 255f, 70 / 255f, 1f);
            menuButtonTheme.Pressed.FillColor = DefaultTheme.Accent;

            var menuButtonDef = new ButtonDefinition
            {
                Theme = menuButtonTheme,
                AutoWidth = true,
                TextMargin = new Vector2(10, 0),
                Size = new Vector2(0, menuBarHeight),
                TextAlignment = new Alignment(HAlignment.Center, VAlignment.Center)
            };

            UI.BeginHBoxContainer("menu_bar", new Vector2(5, 0), 0);
            menuButtonDef.Text = "File";
            if (UI.Button("file_button", menuButtonDef)) { Console.WriteLine("File clicked"); }

            menuButtonDef.Text = "Project";
            if (UI.Button("project_button", menuButtonDef))
            {
                if (!_isProjectWindowOpen)
                {
                    _openProjectWindowRequested = true;
                }
            }

            menuButtonDef.Text = "Edit";
            if (UI.Button("edit_button", menuButtonDef)) { Console.WriteLine("Edit clicked"); }
            menuButtonDef.Text = "View";
            if (UI.Button("view_button", menuButtonDef)) { Console.WriteLine("View clicked"); }
            menuButtonDef.Text = "Help";
            if (UI.Button("help_button", menuButtonDef)) { Console.WriteLine("Help clicked"); }
            UI.EndHBoxContainer();
        }

        // --- Define shared styles ---
        var buttonTheme = new ButtonStylePack
        {
            Roundness = 0.2f,
            BorderLength = 1, // Thinner border
            FontName = "Segoe UI",
            FontSize = 16,
        };

        var panelStyle = new BoxStyle
        {
            BorderLength = 1,
            Roundness = 0f
        };

        var vPanelDef = new ResizablePanelDefinition
        {
            MinWidth = 150,
            MaxWidth = 400,
            Padding = new Vector2(10, 10),
            Gap = 10,
            PanelStyle = panelStyle
        };

        var hPanelDef = new ResizableHPanelDefinition
        {
            MinHeight = 50,
            MaxHeight = 300,
            Padding = new Vector2(10, 10),
            Gap = 10,
            PanelStyle = panelStyle
        };


        // --- Left Panel ---
        {
            UI.BeginResizableVPanel("left_panel", ref leftPanelWidth, vPanelDef, HAlignment.Left, menuBarHeight);

            // Wrap tree in a VBox with 0 gap to ensure lines connect correctly
            UI.BeginVBoxContainer("tree_vbox", UI.Context.GetCurrentLayoutPosition(), 0);
            UI.Tree("file_tree", _fileRoot, out var clickedNode, _treeStyle);
            if (clickedNode is not null)
            {
                Console.WriteLine($"Tree Node Clicked: '{clickedNode.Text}', Path: {clickedNode.UserData}");
            }
            UI.EndVBoxContainer();

            UI.EndResizableVPanel();
        }

        // --- Right Panel ---
        {
            UI.BeginResizableVPanel("right_panel", ref rightPanelWidth, vPanelDef, HAlignment.Right, menuBarHeight);

            if (UI.Button("right_button_1", new ButtonDefinition { Text = "Right Panel", Theme = buttonTheme }))
            {
                Console.WriteLine("Right panel button 1 clicked!");
            }
            if (UI.Button("right_button_2", new ButtonDefinition { Text = "Another Button", Theme = buttonTheme }))
            {
                Console.WriteLine("Right panel button 2 clicked!");
            }
            sliderValue = UI.HSlider("my_slider", sliderValue, new SliderDefinition { Size = new Vector2(200, 20) });

            UI.EndResizableVPanel();
        }

        // --- Bottom Panel ---
        {
            UI.BeginResizableHPanel("bottom_panel", ref bottomPanelHeight, hPanelDef, leftPanelWidth, rightPanelWidth, menuBarHeight);

            if (UI.Button("bottom_button", new ButtonDefinition { Text = "Bottom Panel Button", Theme = buttonTheme }))
            {
                Console.WriteLine("Bottom button clicked!");
            }

            UI.EndResizableHPanel();
        }
    }

    // Drawing logic for the modal window.
    private void DrawProjectWindowUI(UIContext context)
    {
        UI.BeginVBoxContainer("modal_vbox", new Vector2(10, 10), 10);

        if (UI.Button("modal_button_1", new ButtonDefinition { Text = "A button in a modal" }))
        {
            Console.WriteLine("Modal button clicked!");
        }

        if (UI.Button("modal_button_close", new ButtonDefinition { Text = "Close Me" }))
        {
            _isProjectWindowOpen = false; // Signal to close the window
            _projectWindow?.Close();
        }

        UI.EndVBoxContainer();
    }
}