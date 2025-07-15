// GraphicsDevice.cs
using System;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using SharpGen.Runtime;
using Vortice.Mathematics;
using Vortice.DCommon;
using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

public class DuiGraphicsDevice : IDisposable
{
    public ID2D1Factory1? D2DFactory => SharedGraphicsResources.D2DFactory;
    public IDWriteFactory? DWriteFactory => SharedGraphicsResources.DWriteFactory;
    public ID2D1RenderTarget? RenderTarget { get; private set; }
    public ID3D11Device? D3DDevice { get; private set; }
    public ID3D11DeviceContext? D3DContext { get; private set; }
    public IDXGISwapChain? SwapChain { get; private set; }
    public ID3D11DepthStencilView? DepthStencilView { get; private set; } // Added
    public bool IsInitialized { get; private set; } = false;

    private ID3D11Texture2D? _depthStencilBuffer; // Added
    private bool _isDisposed = false;

    public bool Initialize(IntPtr hwnd, SizeI size)
    {
        if (IsInitialized) return true;
        if (hwnd == IntPtr.Zero) return false;

        Console.WriteLine($"Attempting Graphics Initialization for HWND {hwnd} with size {size}...");
        try
        {
            // Clean up any previous (potentially invalid) instance resources
            CleanupRenderTarget();

            if (D2DFactory is null || DWriteFactory is null)
            {
                throw new InvalidOperationException("Shared graphics factories are not initialized. Application.Run() must be called first.");
            }

            if (size.Width <= 0 || size.Height <= 0)
            {
                Console.WriteLine($"Invalid client rect size ({size}). Aborting graphics initialization.");
                return false;
            }

            var swapChainDesc = new SwapChainDescription()
            {
                BufferCount = 1,
                BufferDescription = new ModeDescription((uint)size.Width, (uint)size.Height, Format.B8G8R8A8_UNorm),
                BufferUsage = Usage.RenderTargetOutput,
                OutputWindow = hwnd,
                SampleDescription = new SampleDescription(1, 0),
                Windowed = true
            };

            D3D11.D3D11CreateDeviceAndSwapChain(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                null,
                swapChainDesc,
                out var swapChain,
                out var d3dDevice,
                out _,
                out var d3dContext).CheckError();

            SwapChain = swapChain;
            D3DDevice = d3dDevice;
            D3DContext = d3dContext;

            using (var backBuffer = SwapChain.GetBuffer<ID3D11Texture2D>(0))
            using (var surface = backBuffer.QueryInterface<IDXGISurface>())
            {
                var renderTargetProperties = new RenderTargetProperties(new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied));
                RenderTarget = D2DFactory.CreateDxgiSurfaceRenderTarget(surface, renderTargetProperties);
            }

            CreateDepthStencilView(size); // Create depth buffer

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

    private void CreateDepthStencilView(SizeI size)
    {
        if (D3DDevice is null) return;

        // Clean up old resources first
        _depthStencilBuffer?.Dispose();
        DepthStencilView?.Dispose();

        var depthStencilDesc = new Texture2DDescription
        {
            Width = size.Width,
            Height = size.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.D24_UNorm_S8_UInt,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil,
        };
        _depthStencilBuffer = D3DDevice.CreateTexture2D(depthStencilDesc);
        DepthStencilView = D3DDevice.CreateDepthStencilView(_depthStencilBuffer);
    }


    public void Resize(SizeI newSize)
    {
        if (!IsInitialized || RenderTarget is null || SwapChain is null) return;

        try
        {
            Console.WriteLine($"Resizing render target to {newSize}...");
            RenderTarget.Dispose();

            // Also dispose depth resources before resizing
            DepthStencilView?.Dispose();
            _depthStencilBuffer?.Dispose();

            SwapChain.ResizeBuffers(1, (uint)newSize.Width, (uint)newSize.Height, Format.B8G8R8A8_UNorm, 0);
            using (var backBuffer = SwapChain.GetBuffer<ID3D11Texture2D>(0))
            using (var surface = backBuffer.QueryInterface<IDXGISurface>())
            {
                var renderTargetProperties = new RenderTargetProperties(new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied));
                RenderTarget = D2DFactory.CreateDxgiSurfaceRenderTarget(surface, renderTargetProperties);
            }

            // Recreate depth resources with the new size
            CreateDepthStencilView(newSize);

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
        if (!IsInitialized || RenderTarget is null)
        {
            return;
        }
        RenderTarget.BeginDraw();
    }

    public void EndDraw()
    {
        if (!IsInitialized || RenderTarget is null) return;

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
            SwapChain?.Present(1, 0);
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

    private void CleanupRenderTarget()
    {
        RenderTarget?.Dispose();
        RenderTarget = null;
    }

    public void Cleanup()
    {
        bool resourcesExisted = RenderTarget is not null;
        if (resourcesExisted) Console.WriteLine("Cleaning up GraphicsDevice instance resources...");

        CleanupRenderTarget();
        DepthStencilView?.Dispose();
        DepthStencilView = null;
        _depthStencilBuffer?.Dispose();
        _depthStencilBuffer = null;
        D3DContext?.Dispose();
        D3DContext = null;
        D3DDevice?.Dispose();
        D3DDevice = null;
        SwapChain?.Dispose();
        SwapChain = null;
        IsInitialized = false;

        if (resourcesExisted) Console.WriteLine("Finished cleaning GraphicsDevice instance resources.");
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        Cleanup();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~DuiGraphicsDevice()
    {
        Dispose();
    }
}