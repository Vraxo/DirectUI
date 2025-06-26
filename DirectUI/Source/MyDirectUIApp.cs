// MODIFIED: MyDirectUIApp.cs
// Summary: Applied user's change to buttonTheme initialization.
using System;
using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public class MyDirectUIApp : Direct2DAppWindow
{
    private readonly ButtonStylePack buttonTheme;
    private readonly ButtonStylePack specialButtonTheme;
    private readonly SliderStyle sliderTheme;
    private readonly ButtonStylePack sliderGrabberTheme;
    private float horizontalSliderValue = 0.5f;
    private float verticalSliderValue = 0.25f; // Keep this for potential future use
    private float nestedSliderValue = 0.75f; // Keep this for potential future use
    private float gridSlider1 = 0.1f;
    private float gridSlider2H = 0.9f; // Renamed for clarity, now horizontal

    public MyDirectUIApp(string title = "My DirectUI App", int width = 800, int height = 600)
        : base(title, width, height)
    {
        // --- Button Theme (User's version from prompt) ---
        buttonTheme = new ButtonStylePack()
        {
            Roundness = 1f, // Changed from 0f
            BorderLength = 1.0f, // Sets all 4 sides initially
            FontSize = 14f,
            Normal = { BorderLengthBottom = 10 } // Specific override
        };
        // Note: The explicit Normal assignment might override the general BorderLength = 1.0f for the Normal state's bottom border.

        // --- Special Theme ---
        specialButtonTheme = new()
        {
            Roundness = 1f,
            BorderLength = 0.0f,
            FontSize = 14f,
            FontWeight = FontWeight.Bold,
            Normal = { FillColor = new Color4(0.3f, 0.5f, 0.3f, 1.0f), BorderColor = Colors.Transparent },
            Hover = { FillColor = new Color4(0.4f, 0.6f, 0.4f, 1.0f), BorderColor = Colors.Transparent },
            Pressed = { FillColor = new Color4(0.2f, 0.4f, 0.2f, 1.0f), BorderColor = Colors.Transparent },
            Disabled = { FillColor = DefaultTheme.DisabledFill, FontColor = DefaultTheme.DisabledText, BorderColor = Colors.Transparent }
        };

        // --- Slider Themes ---
        sliderTheme = new()
        {
            Background =
            {
                Roundness = 0.2f,
                FillColor = new Color4(0.2f, 0.2f, 0.25f, 1.0f),
                BorderLength = 0 // Uses new property
            },
            Foreground =
            {
                Roundness = 0.2f,
                FillColor = DefaultTheme.Accent,
                BorderLength = 0 // Uses new property
            }
        };

        sliderGrabberTheme = new()
        {
            Roundness = 0.5f,
            BorderLength = 1.0f, // Uses new property
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
        Vector2 gridAvailableSize = new(float.Max(1, availableGridWidth), float.Max(1, availableGridHeight));
        int numberOfColumns = 3;
        Vector2 cellGap = new(10, 10);

        UI.BeginGridContainer(
            "MainGrid",
            new(gridStartX, gridStartY),
            gridAvailableSize,
            numberOfColumns,
            cellGap);

        // --- Grid Content ---

        // Row 1
        UI.Button("GridBtn1", new() 
        { 
            Size = new(100, 30), 
            Text = "Grid Cell 1",
            Theme = buttonTheme
        });

        UI.Button("GridBtnA", new()
        {
            Size = new(100, 30),
            Text = "Grid Cell 2",
            Theme = buttonTheme
        });

        UI.Button("GridBtnB", new() 
        {
            Size = new(100, 30),
            Text = "Grid Cell 3",
            Theme = buttonTheme
        });

        // Row 2: Two HSliders next to each other
        gridSlider1 = UI.HSlider("GridSlider1", gridSlider1, new() 
        { 
            Size = new(150, 20), 
            Theme = sliderTheme, 
            GrabberTheme = sliderGrabberTheme 
        });

        gridSlider2H = UI.HSlider("GridSlider2H", gridSlider2H, new() 
        {
            Size = new(150, 20),
            Theme = sliderTheme,
            GrabberTheme = sliderGrabberTheme
        });

        UI.Button("GridBtn4", new() { Size = new(100, 30), Text = "Grid Cell 6", Theme = buttonTheme });


        // Row 3 (Special buttons)
        UI.Button("GridBtn7", new() { Size = new(80, 25), Text = "Cell 7", Theme = specialButtonTheme });
        UI.Button("GridBtn8", new() { Size = new(120, 25), Text = "Cell 8 - Wider", Theme = specialButtonTheme });
        UI.Button("GridBtn9", new() { Size = new(80, 25), Text = "Cell 9", Theme = specialButtonTheme });

        UI.EndGridContainer();

        UI.EndFrame();
    }
}