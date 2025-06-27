// SharedGraphicsResources.cs
using System;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using SharpGen.Runtime;
using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;

namespace DirectUI;

/// <summary>
/// Manages graphics resources that are shared across the entire application,
/// such as the main Direct2D and DirectWrite factories.
/// </summary>
internal static class SharedGraphicsResources
{
    public static ID2D1Factory1? D2DFactory { get; private set; }
    public static IDWriteFactory? DWriteFactory { get; private set; }

    private static bool s_isInitialized = false;

    /// <summary>
    /// Initializes the shared factories. Should be called once at application startup.
    /// </summary>
    public static void Initialize()
    {
        if (s_isInitialized) return;

        try
        {
            Result factoryResult = D2D1.D2D1CreateFactory(D2D.FactoryType.SingleThreaded, out ID2D1Factory1? d2dFactory);
            factoryResult.CheckError();
            D2DFactory = d2dFactory ?? throw new InvalidOperationException("Shared D2D Factory creation failed silently.");

            Result dwriteResult = DW.DWrite.DWriteCreateFactory(DW.FactoryType.Shared, out IDWriteFactory? dwriteFactory);
            dwriteResult.CheckError();
            DWriteFactory = dwriteFactory ?? throw new InvalidOperationException("Shared DWrite Factory creation failed silently.");

            Console.WriteLine("Shared Graphics Factories Initialized.");
            s_isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL: Could not initialize shared graphics resources: {ex.Message}");
            Cleanup();
            throw;
        }
    }

    /// <summary>
    /// Disposes of the shared factories. Should be called once when the application is closing.
    /// </summary>
    public static void Cleanup()
    {
        if (!s_isInitialized) return;
        Console.WriteLine("Cleaning up shared graphics factories...");
        DWriteFactory?.Dispose();
        DWriteFactory = null;
        D2DFactory?.Dispose();
        D2DFactory = null;
        s_isInitialized = false;
        Console.WriteLine("Shared graphics factories cleaned up.");
    }
}