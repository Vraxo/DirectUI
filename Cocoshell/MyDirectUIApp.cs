using System;
using System.Numerics;
using DirectUI;

namespace Cocoshell;

public class MyDirectUIApp : Direct2DAppWindow
{
    private float sliderValue = 0.5f;
    private float panelWidth = 250f;

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

        var panelDef = new ResizablePanelDefinition
        {
            MinWidth = 150,
            MaxWidth = 400,
            Padding = new Vector2(10, 10),
            Gap = 10
        };

        // --- UI Layout ---
        UI.BeginResizableVPanel("left_panel", ref panelWidth, panelDef);

        // Button
        if (UI.Button("my_button", new ButtonDefinition { Text = "Click Me!", Theme = buttonTheme }))
        {
            Console.WriteLine("Button was clicked!");
        }

        // Slider
        sliderValue = UI.HSlider("my_slider", sliderValue, new SliderDefinition { Size = new Vector2(200, 20) });

        // A second button to show slider value
        if (UI.Button("another_button", new ButtonDefinition { Text = $"Slider: {sliderValue:F2}", Theme = buttonTheme, AutoWidth = true }))
        {
            Console.WriteLine("Second button clicked!");
        }

        UI.EndResizableVPanel();

        // --- End of UI ---
        // Must call EndFrame after all UI calls
        UI.EndFrame();
    }
}