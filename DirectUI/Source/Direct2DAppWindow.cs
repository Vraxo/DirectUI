// Direct2DAppWindow.cs
using System;
using System.Numerics;
using DirectUI.Input;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

public abstract class Direct2DAppWindow : Win32Window
{
    protected AppHost? _appHost;

    protected Direct2DAppWindow(string title = "Vortice DirectUI Base Window", int width = 800, int height = 600)
        : base(title, width, height)
    {
    }

    /// <summary>
    /// Factory method for the derived class to create its specific AppHost.
    /// The AppHost contains the rendering logic and graphics resources.
    /// </summary>
    protected abstract AppHost CreateAppHost();

    protected override bool Initialize()
    {
        Console.WriteLine("Direct2DAppWindow initializing...");
        _appHost = CreateAppHost();
        return _appHost.Initialize(Handle, GetClientRectSize());
    }

    protected override void Cleanup()
    {
        Console.WriteLine("Direct2DAppWindow cleaning up its resources...");
        _appHost?.Cleanup();
        _appHost = null;
    }

    protected override void OnPaint()
    {
        _appHost?.Render();
    }

    public override void FrameUpdate()
    {
        Invalidate(); // Always invalidate to trigger a paint message for a continuous render loop.
    }

    protected override void OnSize(int width, int height)
    {
        _appHost?.Resize(new SizeI(width, height));
    }

    protected override void OnMouseMove(int x, int y)
    {
        _appHost?.Input.SetMousePosition(x, y);
        Invalidate();
    }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        _appHost?.Input.SetMousePosition(x, y); // Update position on click
        _appHost?.Input.SetMouseDown(button);
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        _appHost?.Input.SetMousePosition(x, y); // Update position on release
        _appHost?.Input.SetMouseUp(button);
        Invalidate();
    }

    protected override void OnKeyDown(Keys key)
    {
        _appHost?.Input.AddKeyPressed(key);

        if (key == Keys.Escape)
        {
            Close();
        }
        Invalidate();
    }

    protected override void OnKeyUp(Keys key)
    {
        _appHost?.Input.AddKeyReleased(key);
        Invalidate();
    }

    protected override void OnChar(char c)
    {
        _appHost?.Input.AddCharacterInput(c);
        Invalidate();
    }

    protected override bool OnClose() { return true; }

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