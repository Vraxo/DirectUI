// MyDirectUIApp.cs
using System;
using System.Numerics;
using Vortice.Mathematics; // For Color4 randomization

namespace DirectUI;

// Derived application class holding specific UI elements and logic
public class MyDirectUIApp : Direct2DAppWindow
{
    // --- UI Elements for this specific app ---
    private readonly Button _myTestButton;

    public MyDirectUIApp(string title = "My DirectUI App", int width = 800, int height = 600)
        : base(title, width, height)
    {
        _myTestButton = new Button
        {
            Position = new Vector2(50, 50),
            Size = new Vector2(150, 40),
            Text = "随机"
        };

        _myTestButton.Clicked += HandleTestButtonClick;
        _myTestButton.MouseEntered += (sender) => Console.WriteLine($"Event Handler: Mouse entered Button '{sender.Text}'");
        _myTestButton.MouseExited += (sender) => Console.WriteLine($"Event Handler: Mouse exited Button '{sender.Text}'");
    }

    // Override the virtual method to draw this application's UI
    // The parameters passed from the base class are named 'context' and 'input' here.
    protected override void DrawUIContent(DrawingContext context, InputState input)
    {
        // Process and Draw UI Elements using the static UI class methods
        if (_myTestButton is not null)
        {
            // Use the parameter name 'input' when calling UI.DoButton
            bool clicked = UI.DoButton(context, input, _myTestButton);

            // Click handling can be done via the event handler HandleTestButtonClick
            // if (clicked) { ... }
        }

        // Add calls for other UI elements here
    }

    // --- Event Handlers for UI Elements ---

    private void HandleTestButtonClick(Button sender)
    {
        Console.WriteLine($"Event Handler: Button '{sender.Text}' was clicked! Changing background.");
        _backgroundColor = new Color4(
            (float)Random.Shared.NextDouble(),
            (float)Random.Shared.NextDouble() * 0.5f + 0.2f,
            (float)Random.Shared.NextDouble() * 0.5f + 0.2f,
            1.0f
        );
        sender.Text = "Clicked!";
    }

    // Override other base methods if needed
}