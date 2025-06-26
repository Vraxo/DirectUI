using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI
{
    public class MyDirectUIApp : Direct2DAppWindow
    {
        private float sliderValue = 0.5f;
        private float leftPanelWidth = 250f;
        private float rightPanelWidth = 250f;
        private float bottomPanelHeight = 150f;

        private readonly TreeNode<string> _fileRoot;
        private readonly TreeStyle _treeStyle = new();


        // Constructor to match the call in Program.cs
        public MyDirectUIApp(string title, int width, int height)
            : base(title, width, height)
        {
            // Set a dark background color that matches the new theme
            backgroundColor = new Color4(21 / 255f, 21 / 255f, 21 / 255f, 1.0f); // #151515

            // --- Initialize Tree Data ---
            _fileRoot = new TreeNode<string>("Root", "C:/", true);
            var docs = _fileRoot.AddChild("Documents", "C:/Docs", true);
            docs.AddChild("Work", "C:/Docs/Work");
            var proj = docs.AddChild("Projects", "C:/Docs/Projects", true);
            proj.AddChild("DirectUI", "C:/Docs/Projects/DirectUI");
            proj.AddChild("Old Stuff", "C:/Docs/Projects/Old");
            docs.AddChild("Personal", "C:/Docs/Personal");

            var downloads = _fileRoot.AddChild("Downloads", "C:/Downloads");
            downloads.AddChild("Installer.exe", "C:/Downloads/Installer.exe");
            downloads.AddChild("archive.zip", "C:/Downloads/archive.zip");
        }

        protected override void DrawUIContent(DrawingContext context, InputState input)
        {
            // Must call BeginFrame before any UI calls
            UI.BeginFrame(context, input);

            float menuBarHeight = 30f;

            // --- Menu Bar ---
            var rt = context.RenderTarget;
            var menuBarBackgroundBrush = UI.GetOrCreateBrush(new Color4(37 / 255f, 37 / 255f, 38 / 255f, 1f));
            var menuBarBorderBrush = UI.GetOrCreateBrush(DefaultTheme.NormalBorder);

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
            menuButtonDef.Text = "Edit";
            if (UI.Button("edit_button", menuButtonDef)) { Console.WriteLine("Edit clicked"); }
            menuButtonDef.Text = "View";
            if (UI.Button("view_button", menuButtonDef)) { Console.WriteLine("View clicked"); }
            menuButtonDef.Text = "Help";
            if (UI.Button("help_button", menuButtonDef)) { Console.WriteLine("Help clicked"); }
            UI.EndHBoxContainer();


            // --- Define some styles ---
            var buttonTheme = new ButtonStylePack
            {
                Roundness = 0.2f,
                BorderLength = 1, // Thinner border
                FontName = "Segoe UI",
                FontSize = 16,
            };

            // This style now inherits its colors from the DefaultTheme
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
            UI.BeginResizableVPanel("left_panel", ref leftPanelWidth, vPanelDef, HAlignment.Left, menuBarHeight);

            UI.Tree("file_tree", _fileRoot, out var clickedNode, _treeStyle);
            if (clickedNode is not null)
            {
                Console.WriteLine($"Tree Node Clicked: '{clickedNode.Text}', Path: {clickedNode.UserData}");
            }

            UI.EndResizableVPanel();

            // --- Right Panel ---
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

            // --- Bottom Panel ---
            UI.BeginResizableHPanel("bottom_panel", ref bottomPanelHeight, hPanelDef, leftPanelWidth, rightPanelWidth, menuBarHeight);
            if (UI.Button("bottom_button", new ButtonDefinition { Text = "Bottom Panel Button", Theme = buttonTheme }))
            {
                Console.WriteLine("Bottom button clicked!");
            }
            UI.EndResizableHPanel();

            // --- End of UI ---
            // Must call EndFrame after all UI calls
            UI.EndFrame();
        }
    }
}