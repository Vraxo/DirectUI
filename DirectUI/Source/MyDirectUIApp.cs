// MODIFIED: MyDirectUIApp.cs
// Summary: Added HSlider and VSlider examples.
using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public class MyDirectUIApp : Direct2DAppWindow
{
    private readonly ButtonStylePack buttonTheme;
    private readonly ButtonStylePack specialButtonTheme;
    private readonly SliderStyle sliderTheme;
    private readonly ButtonStylePack sliderGrabberTheme;


    // State for sliders
    private float horizontalSliderValue = 0.5f;
    private float verticalSliderValue = 0.25f;
    private float nestedSliderValue = 0.75f;


    public MyDirectUIApp(string title = "My DirectUI App", int width = 800, int height = 600)
        : base(title, width, height)
    {
        // Button Themes
        buttonTheme = new ButtonStylePack() { Roundness = 0.1f, BorderThickness = 1.0f, FontSize = 14f };
        specialButtonTheme = new ButtonStylePack()
        { /* ... as before ... */
            Roundness = 0.5f,
            BorderThickness = 2.0f,
            FontSize = 14f,
            FontWeight = Vortice.DirectWrite.FontWeight.Bold,
            Normal = { FillColor = new Color4(0.3f, 0.5f, 0.3f, 1.0f), BorderColor = new Color4(0.5f, 0.7f, 0.5f, 1.0f) },
            Hover = { FillColor = new Color4(0.4f, 0.6f, 0.4f, 1.0f), BorderColor = new Color4(0.6f, 0.8f, 0.6f, 1.0f) },
            Pressed = { FillColor = new Color4(0.2f, 0.4f, 0.2f, 1.0f), BorderColor = new Color4(0.4f, 0.6f, 0.4f, 1.0f) }
        };

        // Slider Themes
        sliderTheme = new SliderStyle()
        {
            Background = { Roundness = 0.2f, FillColor = new Color4(0.2f, 0.2f, 0.25f, 1.0f), BorderThickness = 0 },
            Foreground = { Roundness = 0.2f, FillColor = DefaultTheme.Accent, BorderThickness = 0 }
        };
        sliderGrabberTheme = new ButtonStylePack() // Use ButtonStylePack for grabber states
        {
            Roundness = 0.5f, // Make grabber circular/oval
            BorderThickness = 1.0f,
            Normal = { FillColor = new Color4(0.6f, 0.6f, 0.65f, 1.0f), BorderColor = new Color4(0.8f, 0.8f, 0.8f, 1.0f) },
            Hover = { FillColor = new Color4(0.75f, 0.75f, 0.8f, 1.0f), BorderColor = Colors.WhiteSmoke },
            Pressed = { FillColor = Colors.WhiteSmoke, BorderColor = DefaultTheme.Accent }
        };
    }

    protected override void DrawUIContent(DrawingContext context, InputState input)
    {
        UI.BeginFrame(context, input);

        // --- HBox with Buttons (as before) ---
        UI.BeginHBoxContainer("ActionButtons", new Vector2(50, 50), gap: 10.0f);
        if (UI.Button("OkButton", new() { Size = new(84, 28), Text = "OK", Theme = buttonTheme })) { /*...*/ Invalidate(); }
        if (UI.Button("CancelButton", new() { Size = new(84, 28), Text = "Cancel", Theme = buttonTheme })) { /*...*/ }
        UI.Button("DisabledButton", new() { Size = new(100, 28), Text = "Disabled", Theme = buttonTheme, Disabled = true });
        if (UI.Button("AutoWidthButton", new() { Text = "Auto Width", Theme = buttonTheme, AutoWidth = true, Size = new(0, 32) })) { /*...*/ }
        UI.EndHBoxContainer();


        // --- VBox with Vertical Slider ---
        UI.BeginVBoxContainer("VSliderGroup", new Vector2(50, 100), gap: 10f);

        // Add VSlider
        verticalSliderValue = UI.VSlider("VSlider1", verticalSliderValue, new()
        {
            Position = Vector2.Zero, // Position managed by container
            Size = new(20, 150),    // Width, Height
            MinValue = 0.0f,
            MaxValue = 1.0f,
            Step = 0.05f,
            Theme = sliderTheme,
            GrabberTheme = sliderGrabberTheme,
            GrabberSize = new Vector2(20, 10), // Wider than tall grabber
            VerticalDirection = VSliderDirection.BottomToTop // Example direction
        });

        // Maybe add a button below it in the same VBox
        UI.Button("VButtonBelowSlider", new() { Text = $"V Val: {verticalSliderValue:F2}", Size = new(100, 25), Theme = buttonTheme });

        UI.EndVBoxContainer();


        // --- HBox with Horizontal Slider ---
        UI.BeginHBoxContainer("HSliderGroup", new Vector2(150, 100), gap: 10f);

        // Add HSlider
        horizontalSliderValue = UI.HSlider("HSlider1", horizontalSliderValue, new()
        {
            Position = Vector2.Zero, // Position managed by container
            Size = new(200, 20),    // Width, Height
            MinValue = -10.0f,      // Example range
            MaxValue = 10.0f,
            Step = 0.5f,
            Theme = sliderTheme,
            GrabberTheme = sliderGrabberTheme,
            GrabberSize = new Vector2(10, 20) // Taller than wide grabber
                                              // Direction = HSliderDirection.LeftToRight (default)
        });

        // Button next to it
        UI.Button("HButtonNextToSlider", new() { Text = $"H Val: {horizontalSliderValue:F1}", Size = new(100, 25), Theme = buttonTheme });

        UI.EndHBoxContainer();


        // --- Nested Example (HSlider inside VBox) ---
        UI.BeginVBoxContainer("NestedGroup", new Vector2(150, 150), gap: 8f);

        UI.Button("NestedButton1", new() { Size = new(150, 25), Text = "Above Nested", Theme = buttonTheme });

        nestedSliderValue = UI.HSlider("NestedHSlider", nestedSliderValue, new()
        {
            Size = new(150, 18),
            MinValue = 0f,
            MaxValue = 1f,
            Step = 0.1f,
            Theme = sliderTheme,
            GrabberTheme = sliderGrabberTheme,
            GrabberSize = new(12, 12)
        });

        UI.Button("NestedButton2", new() { Size = new(150, 25), Text = $"Nested Val: {nestedSliderValue:F1}", Theme = buttonTheme });

        UI.EndVBoxContainer();


        UI.EndFrame();
    }
}