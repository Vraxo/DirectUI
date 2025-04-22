// MyDirectUIApp.cs
using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public class MyDirectUIApp : Direct2DAppWindow
{
    private readonly ButtonStylePack buttonTheme; // Keep theme reusable

    public MyDirectUIApp(string title = "My DirectUI App", int width = 800, int height = 600)
        : base(title, width, height)
    {
        buttonTheme = new ButtonStylePack()
        {
            Roundness = 0.05f,
        };
    }

    protected override void DrawUIContent(DrawingContext context, InputState input)
    {
        UI.BeginFrame(context, input);


        if (UI.Button("OkButton", new()
        {
            Position = new(50, 50),
            Size = new(84, 28),
            Text = "OK",
            Theme = buttonTheme,
            Disabled = false
        }))
        {
            backgroundColor = new(
                (float)Random.Shared.NextDouble() * 0.8f + 0.1f,
                (float)Random.Shared.NextDouble() * 0.5f + 0.2f,
                (float)Random.Shared.NextDouble() * 0.5f + 0.2f,
                1.0f
            );

            Invalidate();
        }

        if (UI.Button("CancelButton", new()
        {
            Position = new(150, 50),
            Size = new(84, 28),
            Text = "Cancel",
            Theme = buttonTheme,
            Disabled = false
        }))
        {
            Console.WriteLine($"IMGUI Style: Button 'CancelButton' was clicked!");
        }

        UI.Button("DisabledButton", new ButtonDefinition()
        {
            Position = new(250, 50),
            Size = new(100, 28),
            Text = "Disabled",
            Theme = buttonTheme,
            Disabled = true
        });

        UI.EndFrame();
    }
}