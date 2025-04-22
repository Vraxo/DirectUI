// UI.cs
using SharpGen.Runtime;
using System.Collections.Generic;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public static class UI
{
    private static ID2D1HwndRenderTarget? currentRenderTarget;
    private static IDWriteFactory? currentDWriteFactory;
    private static InputState currentInputState;

    // Expose context via public static properties
    public static ID2D1HwndRenderTarget? CurrentRenderTarget => currentRenderTarget;
    public static IDWriteFactory? CurrentDWriteFactory => currentDWriteFactory;
    public static InputState CurrentInputState => currentInputState;


    private static readonly Dictionary<Color4, ID2D1SolidColorBrush> BrushCache = new();

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
        // No need to clear struct currentInputState explicitly
    }

    // Removed DoButton method

    // Now uses the static CurrentRenderTarget property internally
    public static ID2D1SolidColorBrush GetOrCreateBrush(Color4 color)
    {
        if (CurrentRenderTarget is null)
        {
            // This should ideally not happen if BeginFrame/EndFrame are used correctly
            Console.WriteLine("Error: GetOrCreateBrush called with no active render target.");
            return null!;
        }

        if (BrushCache.TryGetValue(color, out ID2D1SolidColorBrush? brush))
        {
            if (brush is not null)
            {
                return brush;
            }
            else
            {
                BrushCache.Remove(color);
            }
        }

        try
        {
            // Use the static property
            brush = CurrentRenderTarget.CreateSolidColorBrush(color);
            if (brush is not null)
            {
                BrushCache[color] = brush;
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
        int count = BrushCache.Count;
        foreach (var pair in BrushCache)
        {
            pair.Value?.Dispose();
        }
        BrushCache.Clear();
        Console.WriteLine($"UI Resource Cleanup finished. Disposed {count} brushes.");
    }
}