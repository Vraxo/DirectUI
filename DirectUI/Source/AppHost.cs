// AppHost.cs
using System;
using System.Numerics;
using DirectUI.Diagnostics;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

/// <summary>
/// Manages the application's rendering lifecycle, graphics device, and input state aggregation.
/// This class acts as the "engine" that is hosted by a window.
/// </summary>
public class AppHost
{
    private readonly Action<UIContext> _drawCallback;
    private readonly Color4 _backgroundColor;
    private readonly FpsCounter _fpsCounter;

    private GraphicsDevice? _graphicsDevice;
    private IntPtr _hwnd;

    // Input state
    private Vector2 _currentMousePos = new(-1, -1);
    private bool _isLeftMouseButtonDown = false;
    private bool _wasLeftMouseClickedThisFrame = false;

    public AppHost(Action<UIContext> drawCallback, Color4 backgroundColor)
    {
        _drawCallback = drawCallback ?? throw new ArgumentNullException(nameof(drawCallback));
        _backgroundColor = backgroundColor;
        _fpsCounter = new FpsCounter();
    }

    public bool Initialize(IntPtr hwnd, SizeI clientSize)
    {
        _hwnd = hwnd;
        if (_graphicsDevice?.IsInitialized ?? false) return true;
        if (_hwnd == IntPtr.Zero) return false;

        _graphicsDevice ??= new GraphicsDevice();

        if (_graphicsDevice.Initialize(_hwnd, clientSize))
        {
            _fpsCounter.Initialize(_graphicsDevice.RenderTarget!, _graphicsDevice.DWriteFactory!);
            return true;
        }

        return false;
    }

    public void Cleanup()
    {
        _fpsCounter.Cleanup();
        UI.CleanupResources();
        _graphicsDevice?.Cleanup();
    }

    public void Resize(SizeI newSize)
    {
        if (_graphicsDevice?.IsInitialized ?? false)
        {
            _graphicsDevice.Resize(newSize);

            if (_graphicsDevice.IsInitialized)
            {
                _fpsCounter.HandleResize(_graphicsDevice.RenderTarget!);
            }
        }
        else if (_hwnd != IntPtr.Zero)
        {
            Initialize(_hwnd, GetClientRectSizeForHost());
        }
    }

    public void UpdateFpsAndInvalidate()
    {
        if (_fpsCounter.Update())
        {
            Invalidate();
        }
    }

    private void Invalidate()
    {
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.InvalidateRect(_hwnd, IntPtr.Zero, false);
        }
    }

    public void Render()
    {
        if (!(_graphicsDevice?.IsInitialized ?? false))
        {
            if (!Initialize(_hwnd, GetClientRectSizeForHost()))
            {
                _wasLeftMouseClickedThisFrame = false;
                return;
            }
        }

        _graphicsDevice!.BeginDraw();

        var rt = _graphicsDevice.RenderTarget!;
        var dwrite = _graphicsDevice.DWriteFactory!;

        try
        {
            rt.Clear(_backgroundColor);

            var inputState = new InputState(
                _currentMousePos,
                _wasLeftMouseClickedThisFrame,
                _isLeftMouseButtonDown
            );

            var uiContext = new UIContext(rt, dwrite, inputState);
            UI.BeginFrame(uiContext);

            _drawCallback(uiContext);

            UI.EndFrame();

            _fpsCounter.Draw(rt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during drawing: {ex}");
            _graphicsDevice.Cleanup();
        }
        finally
        {
            _graphicsDevice.EndDraw();
            _wasLeftMouseClickedThisFrame = false;
        }
    }

    private SizeI GetClientRectSizeForHost()
    {
        if (_hwnd != IntPtr.Zero && NativeMethods.GetClientRect(_hwnd, out NativeMethods.RECT r))
        {
            int width = Math.Max(1, r.right - r.left);
            int height = Math.Max(1, r.bottom - r.top);
            return new SizeI(width, height);
        }
        // Fallback for when the handle is not yet valid during initialization.
        // This size isn't critical as resize will be called immediately after.
        return new SizeI(1, 1);
    }

    public void SetMousePosition(int x, int y)
    {
        _currentMousePos = new Vector2(x, y);
    }

    public void SetMouseDown(MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _isLeftMouseButtonDown = true;
            _wasLeftMouseClickedThisFrame = true;
        }
    }

    public void SetMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _isLeftMouseButtonDown = false;
        }
    }
}