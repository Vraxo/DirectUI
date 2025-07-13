// Win32Window.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DirectUI.Core; // Added for IWindowHost

namespace DirectUI;

public abstract class Win32Window : IDisposable
{
    private readonly string _windowClassName;
    private readonly string _windowTitle;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hInstance = IntPtr.Zero;
    private NativeMethods.WndProc _wndProcDelegate;
    private bool _isDisposed = false;
    private static readonly HashSet<string> RegisteredClassNames = new HashSet<string>();
    private GCHandle _gcHandle;

    public IntPtr Handle => _hwnd;
    public string Title => _windowTitle;
    public int Width { get; protected set; }
    public int Height { get; protected set; }
    protected IntPtr OwnerHandle { get; private set; } = IntPtr.Zero;

    protected Win32Window(string title, int width, int height, string className = null)
    {
        _windowTitle = title ?? "Win32 Window";
        _initialWidth = width > 0 ? width : 800;
        _initialHeight = height > 0 ? height : 600;
        Width = _initialWidth;
        Height = _initialHeight;
        _windowClassName = className ?? ("Win32Window_" + Guid.NewGuid().ToString("N"));
        _wndProcDelegate = WindowProcedure;
    }

    public bool Create(IntPtr owner = default, uint? style = null, int? x = null, int? y = null)
    {
        OwnerHandle = owner;

        if (_hwnd != IntPtr.Zero)
        {
            return true;
        }

        if (!TryCreateWindow(owner, style, x, y))
        {
            Console.WriteLine("Window creation failed.");
            Dispose();
            return false;
        }

        if (!Initialize())
        {
            Console.WriteLine("Derived init failed.");
            Dispose();
            return false;
        }

        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNORMAL);
        NativeMethods.UpdateWindow(_hwnd);

        return true;
    }

    private bool TryCreateWindow(IntPtr owner, uint? style, int? x, int? y)
    {
        _hInstance = NativeMethods.GetModuleHandle(null);

        if (_hInstance == IntPtr.Zero)
        {
            _hInstance = Process.GetCurrentProcess().Handle;
        }

        Application.RegisterWindow(this);

        lock (RegisteredClassNames)
        {
            if (!RegisteredClassNames.Contains(_windowClassName))
            {
                NativeMethods.WNDCLASSEX wndClass = new()
                {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                    style = NativeMethods.CS_HREDRAW | NativeMethods.CS_VREDRAW,
                    lpfnWndProc = _wndProcDelegate,
                    hInstance = _hInstance,
                    lpszClassName = _windowClassName,
                    hIcon = NativeMethods.LoadIcon(IntPtr.Zero, (IntPtr)32512),
                    hCursor = NativeMethods.LoadCursor(IntPtr.Zero, 32512),
                };

                if (NativeMethods.RegisterClassEx(ref wndClass) == 0)
                {
                    Console.WriteLine($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                RegisteredClassNames.Add(_windowClassName);
                Console.WriteLine($"Class '{_windowClassName}' registered.");
            }
        }

        _gcHandle = GCHandle.Alloc(this);

        uint windowStyle = style ?? (NativeMethods.WS_OVERLAPPEDWINDOW | NativeMethods.WS_VISIBLE);

        int finalX = x ?? NativeMethods.CW_USEDEFAULT;
        int finalY = y ?? NativeMethods.CW_USEDEFAULT;

        _hwnd = NativeMethods.CreateWindowEx(
            0,
            _windowClassName,
            _windowTitle,
            windowStyle,
            finalX,
            finalY,
            _initialWidth,
            _initialHeight,
            owner,
            IntPtr.Zero,
            _hInstance,
            GCHandle.ToIntPtr(_gcHandle));

        if (_hwnd == IntPtr.Zero)
        {
            Console.WriteLine($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");

            if (_gcHandle.IsAllocated)
            {
                _gcHandle.Free();
            }

            return false;
        }

        Console.WriteLine($"Window created: {_hwnd}"); return true;
    }

    private static IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        Win32Window? window = null;

        if (msg == NativeMethods.WM_NCCREATE)
        {
            var cs = Marshal.PtrToStructure<NativeMethods.CREATESTRUCT>(lParam);
            var handle = GCHandle.FromIntPtr(cs.lpCreateParams); window = handle.Target as Win32Window;

            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, GCHandle.ToIntPtr(handle));
            Console.WriteLine($"WM_NCCREATE: Associated instance with HWND {hWnd}");
        }
        else
        {
            IntPtr ptr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);

            if (ptr != IntPtr.Zero)
            {
                var handle = GCHandle.FromIntPtr(ptr); window = handle.Target as Win32Window;
            }
        }

        if (window is null)
        {
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        try
        {
            return window.HandleMessage(hWnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling msg {msg}: {ex}");
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    protected virtual IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return msg switch
        {
            NativeMethods.WM_PAINT => HandleWmPaint(),
            NativeMethods.WM_SIZE => HandleWmSize(lParam),
            NativeMethods.WM_MOUSEMOVE => HandleWmMouseMove(lParam),
            NativeMethods.WM_LBUTTONDOWN => HandleWmButtonDown(MouseButton.Left, lParam, hWnd),
            NativeMethods.WM_LBUTTONUP => HandleWmButtonUp(MouseButton.Left, lParam, hWnd),
            NativeMethods.WM_RBUTTONDOWN => HandleWmButtonDown(MouseButton.Right, lParam, hWnd),
            NativeMethods.WM_RBUTTONUP => HandleWmButtonUp(MouseButton.Right, lParam, hWnd),
            NativeMethods.WM_MBUTTONDOWN => HandleWmButtonDown(MouseButton.Middle, lParam, hWnd),
            NativeMethods.WM_MBUTTONUP => HandleWmButtonUp(MouseButton.Middle, lParam, hWnd),
            NativeMethods.WM_XBUTTONDOWN => HandleWmXButtonDown(wParam, lParam, hWnd),
            NativeMethods.WM_XBUTTONUP => HandleWmXButtonUp(wParam, lParam, hWnd),
            NativeMethods.WM_MOUSEWHEEL => HandleWmMouseWheel(wParam),
            NativeMethods.WM_KEYDOWN => HandleWmKey((Keys)wParam),
            NativeMethods.WM_KEYUP => HandleWmKey((Keys)wParam),
            NativeMethods.WM_CHAR => HandleWmChar(wParam),
            NativeMethods.WM_CLOSE => HandleWmClose(hWnd),
            NativeMethods.WM_DESTROY => HandleWmDestroy(),
            NativeMethods.WM_NCDESTROY => HandleWmNcDestroy(hWnd),
            _ => NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam),
        };
    }

    private IntPtr HandleWmPaint()
    {
        OnPaint();
        return IntPtr.Zero;
    }

    private IntPtr HandleWmSize(IntPtr lParam)
    {
        Width = NativeMethods.LoWord(lParam);
        Height = NativeMethods.HiWord(lParam);
        OnSize(Width, Height);
        return IntPtr.Zero;
    }

    private IntPtr HandleWmMouseMove(IntPtr lParam)
    {
        OnMouseMove(NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
        return IntPtr.Zero;
    }

    private IntPtr HandleWmButtonDown(MouseButton button, IntPtr lParam, IntPtr hWnd)
    {
        NativeMethods.SetCapture(hWnd);
        OnMouseDown(button, NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
        return IntPtr.Zero;
    }

    private IntPtr HandleWmButtonUp(MouseButton button, IntPtr lParam, IntPtr hWnd)
    {
        NativeMethods.ReleaseCapture();
        OnMouseUp(button, NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
        return IntPtr.Zero;
    }

    private IntPtr HandleWmXButtonDown(IntPtr wParam, IntPtr lParam, IntPtr hWnd)
    {
        short xButton = NativeMethods.HiWord(wParam);
        var button = (xButton == 1) ? MouseButton.XButton1 : MouseButton.XButton2;
        NativeMethods.SetCapture(hWnd);
        OnMouseDown(button, NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
        return IntPtr.Zero;
    }

    private IntPtr HandleWmXButtonUp(IntPtr wParam, IntPtr lParam, IntPtr hWnd)
    {
        short xButton = NativeMethods.HiWord(wParam);
        var button = (xButton == 1) ? MouseButton.XButton1 : MouseButton.XButton2;
        NativeMethods.ReleaseCapture();
        OnMouseUp(button, NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
        return IntPtr.Zero;
    }

    private IntPtr HandleWmMouseWheel(IntPtr wParam)
    {
        short wheelDelta = NativeMethods.HiWord(wParam);
        OnMouseWheel((float)wheelDelta / 120.0f); // Normalize delta
        return IntPtr.Zero;
    }

    private IntPtr HandleWmKey(Keys key)
    {
        // This method is called for both WM_KEYDOWN and WM_KEYUP.
        // Derived classes override OnKeyDown and OnKeyUp for specific logic.
        // It's the responsibility of the derived class to know if it's a down or up event.
        // For now, this just dispatches without knowing if it's down or up at this level.
        // The original switch handled it implicitly by calling specific OnKeyDown/OnKeyUp.
        // Re-aligning this with the specific OnKeyDown/OnKeyUp methods:
        return NativeMethods.DefWindowProc(Handle, NativeMethods.WM_KEYDOWN, (IntPtr)key, IntPtr.Zero); // Dummy return, actual handling is in specific OnKeyDown/Up
    }

    private IntPtr HandleWmChar(IntPtr wParam)
    {
        OnChar((char)wParam);
        return IntPtr.Zero;
    }

    private IntPtr HandleWmClose(IntPtr hWnd)
    {
        if (OnClose())
        {
            NativeMethods.DestroyWindow(hWnd);
        }
        return IntPtr.Zero;
    }

    private IntPtr HandleWmDestroy()
    {
        Console.WriteLine($"WM_DESTROY for {_hwnd}.");
        Application.UnregisterWindow(this);
        OnDestroy();

        if (OwnerHandle == IntPtr.Zero)
        {
            Application.Exit();
        }
        return IntPtr.Zero;
    }

    private IntPtr HandleWmNcDestroy(IntPtr hWnd)
    {
        Console.WriteLine($"WM_NCDESTROY: Releasing GCHandle for {hWnd}.");

        IntPtr ptr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);

        if (ptr != IntPtr.Zero)
        {
            var handle = GCHandle.FromIntPtr(ptr);

            if (handle.IsAllocated)
            {
                handle.Free();
            }

            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
        }

        if (_gcHandle.IsAllocated && GCHandle.ToIntPtr(_gcHandle) == ptr)
        {
            _gcHandle = default;

        }

        _hwnd = IntPtr.Zero;
        return IntPtr.Zero;
    }


    public void Close()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.DestroyWindow(_hwnd);
    }

    public void Invalidate()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.InvalidateRect(_hwnd, IntPtr.Zero, false);
    }

    public virtual void FrameUpdate()
    {

    }

    internal bool GetWindowRect(out NativeMethods.RECT rect)
    {
        if (Handle == IntPtr.Zero)
        {
            rect = default;
            return false;
        }

        return NativeMethods.GetWindowRect(Handle, out rect);
    }

    protected virtual bool Initialize()
    {
        return true;
    }

    protected abstract void OnPaint();

    protected virtual void OnSize(int width, int height)
    {

    }

    protected virtual void OnMouseDown(MouseButton button, int x, int y)
    {

    }

    protected virtual void OnMouseUp(MouseButton button, int x, int y)
    {

    }

    protected virtual void OnMouseMove(int x, int y)
    {

    }

    protected virtual void OnKeyDown(Keys key)
    {

    }

    protected virtual void OnKeyUp(Keys key)
    {

    }

    protected virtual void OnMouseWheel(float delta)
    {

    }

    protected virtual void OnChar(char c)
    {

    }

    protected virtual bool OnClose()
    {
        return true;
    }

    protected virtual void OnDestroy()
    {

    }

    protected virtual void Cleanup()
    {

    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            Console.WriteLine("Disposing Win32Window (managed)...");
            Cleanup();
        }

        Console.WriteLine("Disposing Win32Window (unmanaged)...");

        if (_hwnd != IntPtr.Zero)
        {
            Console.WriteLine($"Destroying window {_hwnd} during Dispose...");
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
        else
        {
            if (_gcHandle.IsAllocated)
            {
                Console.WriteLine("Freeing dangling GCHandle...");
                _gcHandle.Free();
            }
        }

        _isDisposed = true;

        Console.WriteLine("Win32Window disposed.");
    }

    ~Win32Window()
    {
        Console.WriteLine("Win32Window Finalizer!");
        Dispose(false);
    }
}