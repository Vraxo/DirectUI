using System;
using System.Numerics;

namespace DirectUI
{
    public class MyDirectUIApp : Direct2DAppWindow
    {
        private float sliderValue = 0.5f;
        private float leftPanelWidth = 250f;
        private float rightPanelWidth = 250f;
        private float bottomPanelHeight = 150f;

        // Constructor to match the call in Program.cs
        public MyDirectUIApp(string title, int width, int height)
            : base(title, width, height)
        {
            // Initialization logic for the app can go here
        }

        protected override void DrawUIContent(DrawingContext context, InputState input)
        {
            // Must call BeginFrame before any UI calls
            UI.BeginFrame(context, input);

            // --- Define some styles ---
            var buttonTheme = new ButtonStylePack
            {
                Roundness = 0.2f,
                BorderLength = 2,
                FontName = "Segoe UI",
                FontSize = 16,
            };

            var panelStyle = new BoxStyle
            {
                FillColor = new(0.15f, 0.15f, 0.2f, 1.0f),
                BorderColor = DefaultTheme.NormalBorder,
                BorderLength = 1,
                Roundness = 0f // No roundness
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
            UI.BeginResizableVPanel("left_panel", ref leftPanelWidth, vPanelDef, HAlignment.Left, bottomPanelHeight);
            if (UI.Button("my_button", new ButtonDefinition { Text = "Click Me!", Theme = buttonTheme }))
            {
                Console.WriteLine("Button was clicked!");
            }
            sliderValue = UI.HSlider("my_slider", sliderValue, new SliderDefinition { Size = new Vector2(200, 20) });
            if (UI.Button("another_button", new ButtonDefinition { Text = $"Slider: {sliderValue:F2}", Theme = buttonTheme, AutoWidth = true }))
            {
                Console.WriteLine("Second button clicked!");
            }
            UI.EndResizableVPanel();

            // --- Right Panel ---
            UI.BeginResizableVPanel("right_panel", ref rightPanelWidth, vPanelDef, HAlignment.Right, bottomPanelHeight);
            if (UI.Button("right_button_1", new ButtonDefinition { Text = "Right Panel", Theme = buttonTheme }))
            {
                Console.WriteLine("Right panel button 1 clicked!");
            }
            if (UI.Button("right_button_2", new ButtonDefinition { Text = "Another Button", Theme = buttonTheme }))
            {
                Console.WriteLine("Right panel button 2 clicked!");
            }
            UI.EndResizableVPanel();

            // --- Bottom Panel ---
            UI.BeginResizableHPanel("bottom_panel", ref bottomPanelHeight, hPanelDef, leftPanelWidth, rightPanelWidth);
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