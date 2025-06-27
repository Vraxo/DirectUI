// GraphicsDevice.cs
using System;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using SharpGen.Runtime;
using Vortice.Mathematics;
using Vortice.DCommon;
using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;
using D2DFactoryType = Vortice.Direct2D1.FactoryType;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

public class GraphicsDevice : IDisposable
{
    public ID2D1Factory1? D2DFactory { get; private set; }
    public IDWriteFactory? DWriteFactory { get; private set; }
    public ID2D1HwndRenderTarget? RenderTarget { get; private set; }
    public bool IsInitialized { get; private set; } = false;

    private bool _isDisposed = false;

    public bool Initialize(IntPtr hwnd, SizeI size)
    {
        if (IsInitialized) return true;
        if (hwnd == IntPtr.Zero) return false;

        Console.WriteLine($"Attempting Graphics Initialization for HWND {hwnd} with size {size}...");
        try
        {
            Cleanup(); // Clean up any previous (potentially invalid) resources

            Result factoryResult = D2D1.D2D1CreateFactory(D2DFactoryType.SingleThreaded, out ID2D1Factory1? d2dFactory);
            factoryResult.CheckError();
            D2DFactory = d2dFactory ?? throw new InvalidOperationException("D2D Factory creation failed silently.");

            Result dwriteResult = DWrite.DWriteCreateFactory(DW.FactoryType.Shared, out IDWriteFactory? dwriteFactory);
            dwriteResult.CheckError();
            DWriteFactory = dwriteFactory ?? throw new InvalidOperationException("DWrite Factory creation failed silently.");

            if (size.Width <= 0 || size.Height <= 0)
            {
                Console.WriteLine($"Invalid client rect size ({size}). Aborting graphics initialization.");
                Cleanup();
                return false;
            }

            var dxgiPixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var renderTargetProperties = new RenderTargetProperties(dxgiPixelFormat);
            var hwndRenderTargetProperties = new HwndRenderTargetProperties
            {
                Hwnd = hwnd,
                PixelSize = size,
                PresentOptions = PresentOptions.Immediately
            };

            RenderTarget = D2DFactory.CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties);
            if (RenderTarget is null) throw new InvalidOperationException("Render target creation returned null unexpectedly.");

            RenderTarget.TextAntialiasMode = D2D.TextAntialiasMode.Cleartype;

            Console.WriteLine($"Vortice Graphics initialized successfully for HWND {hwnd}.");
            IsInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Graphics Initialization failed: {ex.Message}");
            Cleanup(); // Ensures we are in a clean state after failure
            return false;
        }
    }

    public void Resize(SizeI newSize)
    {
        if (!IsInitialized || RenderTarget == null) return;

        try
        {
            Console.WriteLine($"Resizing render target to {newSize}...");
            RenderTarget.Resize(newSize);
            Console.WriteLine("Successfully resized render target.");
        }
        catch (SharpGenException ex)
        {
            Console.WriteLine($"Failed to resize Render Target (SharpGenException): {ex.Message} HRESULT: {ex.ResultCode}");
            if (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
            {
                Console.WriteLine("Render target needs recreation (Detected in Resize Exception).");
                MarkAsLost();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to resize Render Target (General Exception): {ex}");
            MarkAsLost();
        }
    }

    public void BeginDraw()
    {
        if (!IsInitialized || RenderTarget == null)
        {
            return;
        }
        RenderTarget.BeginDraw();
    }

    public void EndDraw()
    {
        if (!IsInitialized || RenderTarget == null) return;

        try
        {
            Result endDrawResult = RenderTarget.EndDraw();
            if (endDrawResult.Failure)
            {
                Console.WriteLine($"EndDraw failed: {endDrawResult.Description}");
                if (endDrawResult.Code == D2D.ResultCode.RecreateTarget.Code)
                {
                    Console.WriteLine("Render target needs recreation (Detected in EndDraw).");
                    MarkAsLost();
                }
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Render target needs recreation (Caught SharpGenException in EndDraw): {ex.Message}");
            MarkAsLost();
        }
    }

    private void MarkAsLost()
    {
        if (!IsInitialized) return;
        Console.WriteLine("Marking graphics device as lost. Resources will be recreated on next opportunity.");
        Cleanup();
    }

    public void Cleanup()
    {
        bool resourcesExisted = D2DFactory is not null || RenderTarget is not null || DWriteFactory is not null;
        if (resourcesExisted) Console.WriteLine("Cleaning up GraphicsDevice resources...");

        RenderTarget?.Dispose(); RenderTarget = null;
        DWriteFactory?.Dispose(); DWriteFactory = null;
        D2DFactory?.Dispose(); D2DFactory = null;

        IsInitialized = false;

        if (resourcesExisted) Console.WriteLine("Finished cleaning GraphicsDevice resources.");
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        Cleanup();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~GraphicsDevice()
    {
        Dispose();
    }
}