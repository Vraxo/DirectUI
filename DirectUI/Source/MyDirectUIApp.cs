// MyDirectUIApp.cs
// Removed button field. Register button with UI in constructor.
// Updated DrawUIContent to call UI.UpdateAndDrawAllElements.
using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public class MyDirectUIApp : Direct2DAppWindow
{
    // Removed: private readonly Button button;

    public MyDirectUIApp(string title = "My DirectUI App", int width = 800, int height = 600)
        : base(title, width, height)
    {
        UI.RegisterElement("mainButton", new Button()
        {
            Position = new(50, 50),
            Text = "OK",
            Themes = new()
            {
                Roundness = 0.05f,
                // BorderThickness = 1.0f // Already default, but can set here
            },
            // Assign any other properties like AutoWidth, TextMargin etc.
        });

        // Example of registering another button if needed:
        // Button cancelButton = new() { Position = new(150, 50), Text = "Cancel" };
        // cancelButton.Clicked += (sender) => Console.WriteLine("Cancel Clicked");
        // UI.RegisterElement("cancelButton", cancelButton);
    }

    protected override void DrawUIContent(DrawingContext context, InputState input)
    {
        // BeginFrame might not be strictly necessary now but kept for potential future use
        UI.BeginFrame(context, input);

        // Tell the UI system to update and draw all registered elements
        UI.UpdateAndDrawAllElements(context, input);

        // EndFrame might not be strictly necessary now but kept for potential future use
        UI.EndFrame();
    }

    private void HandleTestButtonClick(Button sender)
    {
        // The 'sender' parameter gives us the button instance that was clicked.
        Console.WriteLine($"Event Handler: Button '{sender.Text}' (ID maybe unknown here) was clicked! Changing background.");
        // Optionally, retrieve the button again by ID if needed, though sender is usually sufficient
        // var clickedButton = UI.GetElement<Button>("mainButton");
        // if (clickedButton is not null && clickedButton == sender) { ... }

        backgroundColor = new Color4(
            (float)Random.Shared.NextDouble() * 0.8f + 0.1f,
            (float)Random.Shared.NextDouble() * 0.5f + 0.2f,
            (float)Random.Shared.NextDouble() * 0.5f + 0.2f,
            1.0f
        );

        // Example: Accessing another element by ID from an event handler
        // var otherButton = UI.GetElement<Button>("cancelButton");
        // if (otherButton is not null)
        // {
        //     otherButton.Disabled = true; // Disable cancel after OK is clicked
        // }
    }

    // Override Cleanup if you need to unregister event handlers explicitly,
    // though clearing the UI elements in UI.CleanupResources might suffice
    // if the Button instances are garbage collected.
    // protected override void Cleanup()
    // {
    //     Button myButton = UI.GetElement<Button>("mainButton");
    //     if (myButton is not null)
    //     {
    //         myButton.Clicked -= HandleTestButtonClick;
    //     }
    //     base.Cleanup(); // Call base cleanup
    // }
}