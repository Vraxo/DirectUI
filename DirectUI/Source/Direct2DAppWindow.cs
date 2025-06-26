// MODIFIED: Direct2DAppWindow.cs
// Summary: Changed PresentOptions from None to Immediately in HwndRenderTargetProperties to attempt disabling VSync throttling.
using System;
using System.Numerics;
using System.Diagnostics;

using Vortice;
using Vortice.Mathematics;

using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using SharpGen.Runtime;

using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;

using D2DFactoryType = Vortice.Direct2D1.FactoryType;
using SizeI = Vortice.Mathematics.SizeI;
using Rect = Vortice.Mathematics.Rect;

using DirectUI;
using DirectUI.Diagnostics;
using Vortice.DCommon;

namespace DirectUI;

public abstract class Direct2DAppWindow : Win32Window
{
    protected ID2D1Factory1? d2dFactory;
    protected IDWriteFactory? dwriteFactory;
    protected ID2D1HwndRenderTarget? renderTarget;

    protected Color4 backgroundColor = new(0.1f, 0.1f, 0.15f, 1.0f);
    protected bool graphicsInitialized = false;

    protected Vector2 currentMousePos = new(-1, -1);
    protected bool isLeftMouseButtonDown = false;
    protected bool wasLeftMouseClickedThisFrame = false;

    // --- FPS Counter ---
    private readonly FpsCounter _fpsCounter;

    public Direct2DAppWindow(string title = "Vortice DirectUI Base Window", int width = 800, int height = 600)
        : base(title, width, height)
    {
        _fpsCounter = new FpsCounter();
    }

    protected override bool Initialize()
    {
        Console.WriteLine("Direct2DAppWindow initializing Vortice Graphics...");
        return InitializeGraphics();
    }

    protected override void Cleanup()
    {
        Console.WriteLine("Direct2DAppWindow cleaning up its resources...");
        CleanupGraphics();
    }

    protected override void OnPaint()
    {
        // Update FPS counter and invalidate if the text changed
        if (_fpsCounter.Update())
        {
            Invalidate();
        }

        if (!graphicsInitialized || renderTarget is null || dwriteFactory is null)
        {
            if (!graphicsInitialized && Handle != nint.Zero)
            {
                InitializeGraphics();
                if (!graphicsInitialized || renderTarget is null || dwriteFactory is null)
                {
                    wasLeftMouseClickedThisFrame = false;
                    return;
                }
            }
            else
            {
                wasLeftMouseClickedThisFrame = false;
                return;
            }
        }

        try
        {
            renderTarget.BeginDraw();
            renderTarget.Clear(backgroundColor);

            var inputState = new InputState(
                currentMousePos,
                wasLeftMouseClickedThisFrame,
                isLeftMouseButtonDown
            );

            var drawingContext = new DrawingContext(renderTarget, dwriteFactory);

            // --- Draw Main UI Content ---
            DrawUIContent(drawingContext, inputState);

            // --- Draw FPS Counter ---
            _fpsCounter.Draw(renderTarget);


            Result endDrawResult = renderTarget.EndDraw();

            if (endDrawResult.Failure)
            {
                Console.WriteLine($"EndDraw failed: {endDrawResult.Description}");
                if (endDrawResult.Code == D2D.ResultCode.RecreateTarget.Code)
                {
                    Console.WriteLine("Render target needs recreation (Detected in EndDraw).");
                    graphicsInitialized = false;
                    CleanupGraphics();
                }
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Render target needs recreation (Caught SharpGenException in OnPaint): {ex.Message}");
            graphicsInitialized = false;
            CleanupGraphics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Rendering Error: {ex}");
            graphicsInitialized = false;
            CleanupGraphics();
        }
        finally
        {
            wasLeftMouseClickedThisFrame = false;
        }
    }

    protected virtual void DrawUIContent(DrawingContext context, InputState input)
    {
        // Base implementation does nothing.
    }

    protected override void OnSize(int width, int height)
    {
        if (graphicsInitialized && renderTarget is not null)
        {
            Console.WriteLine($"Window resized to {width}x{height}. Resizing render target...");
            try
            {
                var newPixelSize = new SizeI(width, height);

                // Resize the render target first.
                renderTarget.Resize(newPixelSize);

                // Then notify components that rely on it.
                _fpsCounter.HandleResize(renderTarget);

                Console.WriteLine($"Successfully resized render target.");
            }
            catch (SharpGenException ex)
            {
                Console.WriteLine($"Failed to resize Render Target (SharpGenException): {ex.Message} HRESULT: {ex.ResultCode}");
                if (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
                {
                    Console.WriteLine("Render target needs recreation (Detected in Resize Exception).");
                    graphicsInitialized = false;
                    CleanupGraphics();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to resize Render Target (General Exception): {ex}");
                graphicsInitialized = false;
                CleanupGraphics();
            }
        }
        else if (!graphicsInitialized && Handle != nint.Zero)
        {
            InitializeGraphics();
        }
    }

    protected override void OnMouseMove(int x, int y) { currentMousePos = new Vector2(x, y); Invalidate(); }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        currentMousePos = new Vector2(x, y);
        if (button == MouseButton.Left)
        {
            isLeftMouseButtonDown = true;
            wasLeftMouseClickedThisFrame = true;
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        currentMousePos = new Vector2(x, y);
        if (button == MouseButton.Left)
        {
            isLeftMouseButtonDown = false;
        }
        Invalidate();
    }

    protected override void OnKeyDown(int keyCode)
    {
        if (keyCode == NativeMethods.VK_ESCAPE)
        {
            Close();
        }
        Invalidate();
    }

    protected override bool OnClose() { return true; }

    protected virtual bool InitializeGraphics()
    {
        if (graphicsInitialized) return true;
        if (Handle == nint.Zero) return false;

        Console.WriteLine($"Attempting Graphics Initialization for HWND {Handle}...");

        try
        {
            CleanupGraphics();

            Result factoryResult = D2D1.D2D1CreateFactory(D2DFactoryType.SingleThreaded, out d2dFactory);
            factoryResult.CheckError();
            if (d2dFactory is null) throw new InvalidOperationException("D2D Factory creation failed silently.");

            Result dwriteResult = DWrite.DWriteCreateFactory(DW.FactoryType.Shared, out dwriteFactory);
            dwriteResult.CheckError();
            if (dwriteFactory is null) throw new InvalidOperationException("DWrite Factory creation failed silently.");

            var clientRectSize = GetClientRectSize();
            if (clientRectSize.Width <= 0 || clientRectSize.Height <= 0)
            {
                Console.WriteLine($"Invalid client rect size ({clientRectSize.Width}x{clientRectSize.Height}). Aborting graphics initialization.");
                CleanupGraphics();
                return false;
            }

            var dxgiPixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var renderTargetProperties = new RenderTargetProperties(dxgiPixelFormat);
            var hwndRenderTargetProperties = new HwndRenderTargetProperties
            {
                Hwnd = Handle,
                PixelSize = new SizeI(clientRectSize.Width, clientRectSize.Height),
                // --- CHANGE HERE ---
                PresentOptions = PresentOptions.Immediately // Attempt to disable VSync throttling
                // --- END CHANGE ---
            };

            renderTarget = d2dFactory.CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties);
            if (renderTarget is null) throw new InvalidOperationException("Render target creation returned null unexpectedly.");

            renderTarget.TextAntialiasMode = D2D.TextAntialiasMode.Cleartype;

            // --- Initialize FPS Counter ---
            _fpsCounter.Initialize(renderTarget, dwriteFactory);


            Console.WriteLine($"Vortice Graphics initialized successfully for HWND {Handle}.");
            graphicsInitialized = true;
            return true;
        }
        catch (SharpGenException ex)
        {
            Console.WriteLine($"Graphics Initialization failed (SharpGenException): {ex.Message} HRESULT: {ex.ResultCode}");
            CleanupGraphics(); graphicsInitialized = false; return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Graphics Initialization failed (General Exception): {ex}");
            CleanupGraphics(); graphicsInitialized = false; return false;
        }
    }

    protected virtual void CleanupGraphics()
    {
        bool resourcesExisted = d2dFactory is not null || renderTarget is not null || dwriteFactory is not null;
        if (resourcesExisted) Console.WriteLine("Cleaning up Vortice Graphics resources...");

        _fpsCounter.Cleanup();

        UI.CleanupResources();

        renderTarget?.Dispose(); renderTarget = null;
        dwriteFactory?.Dispose(); dwriteFactory = null;
        d2dFactory?.Dispose(); d2dFactory = null;
        graphicsInitialized = false;

        if (resourcesExisted) Console.WriteLine("Finished cleaning graphics resources.");
    }

    protected SizeI GetClientRectSize()
    {
        if (Handle != nint.Zero && NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            int width = Math.Max(1, r.right - r.left);
            int height = Math.Max(1, r.bottom - r.top);
            return new SizeI(width, height);
        }
        int baseWidth = Math.Max(1, Width);
        int baseHeight = Math.Max(1, Height);
        if (Handle != nint.Zero)
        {
            Console.WriteLine($"GetClientRect failed. Falling back to stored size: {baseWidth}x{baseHeight}");
        }
        return new SizeI(baseWidth, baseHeight);
    }
}