// MODIFIED: MyDirectUIApp.cs
// Summary: Modified the 'specialButtonTheme' to have BorderThickness = 0 to remove the border artifact.
using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public class MyDirectUIApp : Direct2DAppWindow
{
    // Themes and slider values as before...
    private readonly ButtonStylePack buttonTheme;
    private readonly ButtonStylePack specialButtonTheme;
    private readonly SliderStyle sliderTheme;
    private readonly ButtonStylePack sliderGrabberTheme;
    private float horizontalSliderValue = 0.5f;
    private float verticalSliderValue = 0.25f;
    private float nestedSliderValue = 0.75f;
    private float gridSlider1 = 0.1f;
    private float gridSlider2 = 0.9f;


    public MyDirectUIApp(string title = "My DirectUI App", int width = 800, int height = 600)
        : base(title, width, height)
    {
        // Button Themes
        buttonTheme = new ButtonStylePack()
        {
            Roundness = 0.0f,
            BorderThickness = 1.0f,
            FontSize = 14f,
        };

        // --- Modify Special Theme ---
        specialButtonTheme = new ButtonStylePack()
        {
            Roundness = 0.5f, // Keep rounding
            BorderThickness = 0.0f, // Set border thickness to 0
            FontSize = 14f,
            FontWeight = Vortice.DirectWrite.FontWeight.Bold,
            // Define colors for different states (border colors are now irrelevant but kept for structure)
            Normal = { FillColor = new Color4(0.3f, 0.5f, 0.3f, 1.0f), BorderColor = Colors.Transparent },
            Hover = { FillColor = new Color4(0.4f, 0.6f, 0.4f, 1.0f), BorderColor = Colors.Transparent },
            Pressed = { FillColor = new Color4(0.2f, 0.4f, 0.2f, 1.0f), BorderColor = Colors.Transparent },
            Disabled = { FillColor = DefaultTheme.DisabledFill, FontColor = DefaultTheme.DisabledText, BorderColor = Colors.Transparent } // Ensure disabled state is handled
        };
        // --- End Modify Special Theme ---

        // Slider Themes
        sliderTheme = new SliderStyle()
        {
            Background = { Roundness = 0.2f, FillColor = new Color4(0.2f, 0.2f, 0.25f, 1.0f), BorderThickness = 0 },
            Foreground = { Roundness = 0.2f, FillColor = DefaultTheme.Accent, BorderThickness = 0 }
        };
        sliderGrabberTheme = new ButtonStylePack()
        {
            Roundness = 0.5f,
            BorderThickness = 1.0f, // Keep border on grabber for now
            Normal = { FillColor = new Color4(0.6f, 0.6f, 0.65f, 1.0f), BorderColor = new Color4(0.8f, 0.8f, 0.8f, 1.0f) },
            Hover = { FillColor = new Color4(0.75f, 0.75f, 0.8f, 1.0f), BorderColor = Colors.WhiteSmoke },
            Pressed = { FillColor = Colors.WhiteSmoke, BorderColor = DefaultTheme.Accent }
        };
    }

    protected override void DrawUIContent(DrawingContext context, InputState input)
    {
        UI.BeginFrame(context, input);

        // --- HBox with Buttons (Top Row) ---
        UI.BeginHBoxContainer("ActionButtons", new Vector2(50, 50), gap: 10.0f);
        if (UI.Button("OkButton", new() { Size = new(84, 28), Text = "OK", Theme = buttonTheme })) { backgroundColor = Colors.DarkSlateGray; Invalidate(); }
        if (UI.Button("CancelButton", new() { Size = new(84, 28), Text = "Cancel", Theme = buttonTheme })) { /* log */ }
        UI.EndHBoxContainer();


        // --- Area for Grid ---
        float gridStartX = 50;
        float gridStartY = 100;
        float windowWidth = GetClientRectSize().Width;
        float windowHeight = GetClientRectSize().Height;
        float availableGridWidth = windowWidth - gridStartX - 50;
        float availableGridHeight = windowHeight - gridStartY - 50;
        Vector2 gridAvailableSize = new Vector2(Math.Max(1, availableGridWidth), Math.Max(1, availableGridHeight)); // Ensure non-zero size
        int numberOfColumns = 3;
        Vector2 cellGap = new Vector2(10, 10);

        UI.BeginGridContainer("MainGrid", new Vector2(gridStartX, gridStartY), gridAvailableSize, numberOfColumns, cellGap);

        // Row 1
        UI.Button("GridBtn1", new() { Size = new(100, 30), Text = "Grid Cell 1", Theme = buttonTheme });
        gridSlider1 = UI.HSlider("GridSlider1", gridSlider1, new() { Size = new(150, 20), Theme = sliderTheme, GrabberTheme = sliderGrabberTheme });
        UI.Button("GridBtn2", new() { Size = new(100, 30), Text = "Grid Cell 3", Theme = buttonTheme });

        // Row 2
        UI.Button("GridBtn3", new() { Size = new(100, 50), Text = "Taller Button", Theme = buttonTheme });
        UI.Button("GridBtn4", new() { Size = new(100, 30), Text = "Grid Cell 5", Theme = buttonTheme });
        gridSlider2 = UI.VSlider("GridSlider2", gridSlider2, new() { Size = new(20, 80), Theme = sliderTheme, GrabberTheme = sliderGrabberTheme });

        // Row 3 - Uses specialButtonTheme now without border
        UI.Button("GridBtn5", new() { Size = new(80, 25), Text = "Cell 7", Theme = specialButtonTheme });
        UI.Button("GridBtn6", new() { Size = new(120, 25), Text = "Cell 8 - Wider", Theme = specialButtonTheme });
        UI.Button("GridBtn7", new() { Size = new(80, 25), Text = "Cell 9", Theme = specialButtonTheme }); // This is the one from the image

        UI.EndGridContainer();


        UI.EndFrame();
    }
}