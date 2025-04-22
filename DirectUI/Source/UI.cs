// UI.cs
using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public static class UI
{
    private static ID2D1HwndRenderTarget? currentRenderTarget;
    private static IDWriteFactory? currentDWriteFactory;
    private static InputState currentInputState;
    private static readonly Dictionary<string, object> uiElements = new();
    private static readonly Dictionary<Color4, ID2D1SolidColorBrush> brushCache = new();

    public static ID2D1HwndRenderTarget? CurrentRenderTarget => currentRenderTarget;
    public static IDWriteFactory? CurrentDWriteFactory => currentDWriteFactory;
    public static InputState CurrentInputState => currentInputState;

    public static void BeginFrame(DrawingContext context, InputState input)
    {
        currentRenderTarget = context.RenderTarget;
        currentDWriteFactory = context.DWriteFactory;
        currentInputState = input;
    }

    public static void EndFrame()
    {
        currentRenderTarget = null;
        currentDWriteFactory = null;
    }

    // Updated signature to take ButtonDefinition
    public static bool Button(string id, ButtonDefinition definition)
    {
        if (CurrentRenderTarget is null || CurrentDWriteFactory is null)
        {
            Console.WriteLine($"Error: UI.Button('{id}') called outside BeginFrame/EndFrame.");
            return false;
        }
        if (definition is null)
        {
            Console.WriteLine($"Error: UI.Button('{id}') called with a null definition.");
            return false;
        }

        Button buttonInstance;

        if (uiElements.TryGetValue(id, out object? element) && element is Button existingButton)
        {
            buttonInstance = existingButton;
        }
        else
        {
            Console.WriteLine($"Creating new Button instance for ID: {id}");
            buttonInstance = new Button();
            uiElements[id] = buttonInstance;
        }

        // Apply properties from the definition object
        buttonInstance.Position = definition.Position;
        buttonInstance.Size = definition.Size;
        buttonInstance.Text = definition.Text;

        // Use provided theme or ensure default
        if (definition.Theme is not null)
        {
            buttonInstance.Themes = definition.Theme;
        }
        else if (buttonInstance.Themes is null)
        {
            buttonInstance.Themes = new ButtonStylePack();
        }

        // Apply optional properties using defaults if definition property is null
        buttonInstance.Origin = definition.Origin ?? Vector2.Zero;
        buttonInstance.TextAlignment = definition.TextAlignment ?? new Alignment(HAlignment.Center, VAlignment.Center);
        buttonInstance.TextOffset = definition.TextOffset ?? Vector2.Zero;
        buttonInstance.AutoWidth = definition.AutoWidth;
        buttonInstance.TextMargin = definition.TextMargin ?? new Vector2(10, 5); // Match Button default
        buttonInstance.Behavior = definition.Behavior;
        buttonInstance.LeftClickActionMode = definition.LeftClickActionMode;
        buttonInstance.Disabled = definition.Disabled;
        buttonInstance.UserData = definition.UserData;


        // Button.Update performs state checks, drawing, and returns click status
        bool clicked = buttonInstance.Update();

        return clicked;
    }

    public static ID2D1SolidColorBrush GetOrCreateBrush(Color4 color)
    {
        if (CurrentRenderTarget is null)
        {
            Console.WriteLine("Error: GetOrCreateBrush called with no active render target.");
            return null!;
        }

        if (brushCache.TryGetValue(color, out ID2D1SolidColorBrush? brush))
        {
            if (brush is not null)
            {
                return brush;
            }
            else
            {
                brushCache.Remove(color);
            }
        }

        try
        {
            brush = CurrentRenderTarget.CreateSolidColorBrush(color);
            if (brush is not null)
            {
                brushCache[color] = brush;
                return brush;
            }
            else
            {
                Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}");
                return null!;
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). Clearing cache.");
            CleanupResources();
            return null!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating brush for color {color}: {ex.Message}");
            return null!;
        }
    }

    public static void CleanupResources()
    {
        Console.WriteLine("UI Resource Cleanup: Disposing cached brushes...");
        int count = brushCache.Count;
        foreach (var pair in brushCache)
        {
            pair.Value?.Dispose();
        }
        brushCache.Clear();
        Console.WriteLine($"UI Resource Cleanup finished. Disposed {count} brushes.");

        // Optional: Dispose UI elements if they implement IDisposable
        // foreach(var pair in uiElements)
        // {
        //    if (pair.Value is IDisposable disposableElement)
        //    {
        //        disposableElement.Dispose();
        //    }
        // }
        // uiElements.Clear(); // Be careful if cleanup happens often
    }
}