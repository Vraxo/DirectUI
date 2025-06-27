// Direct2DAppWindow.cs
using System;
using System.Numerics;
using Vortice.Mathematics;
using DirectUI.Diagnostics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

public abstract class Direct2DAppWindow : Win32Window
{
    private GraphicsDevice? _graphicsDevice;

    protected Color4 backgroundColor = new(0.1f, 0.1f, 0.15f, 1.0f);

    protected Vector2 currentMousePos = new(-1, -1);
    protected bool isLeftMouseButtonDown = false;
    protected bool wasLeftMouseClickedThisFrame = false;

    private readonly FpsCounter _fpsCounter;

    public Direct2DAppWindow(string title = "Vortice DirectUI Base Window", int width = 800, int height = 600)
        : base(title, width, height)
    {
        _fpsCounter = new FpsCounter();
    }

    protected override bool Initialize()
    {
        Console.WriteLine("Direct2DAppWindow initializing...");
        return InitializeGraphics();
    }

    protected override void Cleanup()
    {
        Console.WriteLine("Direct2DAppWindow cleaning up its resources...");
        CleanupGraphics();
    }

    protected override void OnPaint()
    {
        if (_fpsCounter.Update())
        {
            Invalidate();
        }

        if (!(_graphicsDevice?.IsInitialized ?? false))
        {
            if (!InitializeGraphics())
            {
                wasLeftMouseClickedThisFrame = false;
                return;
            }
        }

        _graphicsDevice!.BeginDraw();

        var rt = _graphicsDevice.RenderTarget!;
        var dwrite = _graphicsDevice.DWriteFactory!;

        try
        {
            rt.Clear(backgroundColor);

            var inputState = new InputState(
                currentMousePos,
                wasLeftMouseClickedThisFrame,
                isLeftMouseButtonDown
            );

            var drawingContext = new DrawingContext(rt, dwrite);

            DrawUIContent(drawingContext, inputState);

            _fpsCounter.Draw(rt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during drawing: {ex}");
            CleanupGraphics();
        }
        finally
        {
            _graphicsDevice.EndDraw();
            wasLeftMouseClickedThisFrame = false;
        }
    }

    protected virtual void DrawUIContent(DrawingContext context, InputState input)
    {
        // Base implementation does nothing.
    }

    protected override void OnSize(int width, int height)
    {
        if (_graphicsDevice?.IsInitialized ?? false)
        {
            var newPixelSize = new SizeI(width, height);

            _graphicsDevice.Resize(newPixelSize);

            if (_graphicsDevice.IsInitialized)
            {
                _fpsCounter.HandleResize(_graphicsDevice.RenderTarget!);
            }
        }
        else if (Handle != nint.Zero)
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
        if (_graphicsDevice?.IsInitialized ?? false) return true;
        if (Handle == nint.Zero) return false;

        _graphicsDevice ??= new GraphicsDevice();

        if (_graphicsDevice.Initialize(Handle, GetClientRectSize()))
        {
            _fpsCounter.Initialize(_graphicsDevice.RenderTarget!, _graphicsDevice.DWriteFactory!);
            return true;
        }

        return false;
    }

    protected virtual void CleanupGraphics()
    {
        _fpsCounter.Cleanup();
        UI.CleanupResources();

        _graphicsDevice?.Cleanup();
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