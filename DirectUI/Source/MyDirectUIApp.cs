// MODIFIED: MyDirectUIApp.cs
// Summary: Added a GridContainer example.
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
        // Theme setup as before...
        buttonTheme = new ButtonStylePack() { /* ... */ };
        specialButtonTheme = new ButtonStylePack() { /* ... */ };
        sliderTheme = new SliderStyle() { /* ... */ };
        sliderGrabberTheme = new ButtonStylePack() { /* ... */ };
    }

    protected override void DrawUIContent(DrawingContext context, InputState input)
    {
        UI.BeginFrame(context, input);

        // --- HBox with Buttons (Top Row) ---
        UI.BeginHBoxContainer("ActionButtons", new Vector2(50, 50), gap: 10.0f);
        if (UI.Button("OkButton", new() { Size = new(84, 28), Text = "OK", Theme = buttonTheme })) { backgroundColor = Colors.DarkSlateGray; Invalidate(); }
        if (UI.Button("CancelButton", new() { Size = new(84, 28), Text = "Cancel", Theme = buttonTheme })) { /* log */ }
        // Add more buttons if needed...
        UI.EndHBoxContainer();


        // --- Area for Grid ---
        float gridStartX = 50;
        float gridStartY = 100;
        // Calculate available size dynamically based on window size (example)
        float windowWidth = GetClientRectSize().Width; // Get current window width
        float windowHeight = GetClientRectSize().Height;
        float availableGridWidth = windowWidth - gridStartX - 50; // Leave 50px margin on right
        float availableGridHeight = windowHeight - gridStartY - 50; // Leave 50px margin on bottom
        Vector2 gridAvailableSize = new Vector2(availableGridWidth, availableGridHeight);
        int numberOfColumns = 3;
        Vector2 cellGap = new Vector2(10, 10); // 10px horizontal and vertical gap

        UI.BeginGridContainer("MainGrid", new Vector2(gridStartX, gridStartY), gridAvailableSize, numberOfColumns, cellGap);

        // Add elements to the grid (they will flow left-to-right, top-to-bottom)

        // Row 1
        UI.Button("GridBtn1", new() { Size = new(100, 30), Text = "Grid Cell 1", Theme = buttonTheme });
        gridSlider1 = UI.HSlider("GridSlider1", gridSlider1, new() { Size = new(150, 20), Theme = sliderTheme, GrabberTheme = sliderGrabberTheme });
        UI.Button("GridBtn2", new() { Size = new(100, 30), Text = "Grid Cell 3", Theme = buttonTheme });

        // Row 2
        UI.Button("GridBtn3", new() { Size = new(100, 50), Text = "Taller Button", Theme = buttonTheme }); // Taller button will define row height
        UI.Button("GridBtn4", new() { Size = new(100, 30), Text = "Grid Cell 5", Theme = buttonTheme });
        gridSlider2 = UI.VSlider("GridSlider2", gridSlider2, new() { Size = new(20, 80), Theme = sliderTheme, GrabberTheme = sliderGrabberTheme }); // Taller slider

        // Row 3
        UI.Button("GridBtn5", new() { Size = new(80, 25), Text = "Cell 7", Theme = specialButtonTheme });
        UI.Button("GridBtn6", new() { Size = new(120, 25), Text = "Cell 8 - Wider", Theme = specialButtonTheme });
        UI.Button("GridBtn7", new() { Size = new(80, 25), Text = "Cell 9", Theme = specialButtonTheme });


        // Add more widgets as needed... They will continue filling the grid.
        // Example: Add widget larger than calculated cell width
        // UI.Button("OverflowBtn", new() { Size = new(300, 30), Text = "This button might overflow its cell visually" });


        UI.EndGridContainer(); // End the grid layout


        UI.EndFrame();
    }
}