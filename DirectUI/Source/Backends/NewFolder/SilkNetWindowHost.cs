using DirectUI.Core;
using DirectUI.Input;
using Silk.NET.Maths;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI.Backends.SkiaSharp
{
    public enum WindowBackdropType { Default, Mica, Acrylic, Tabbed }
    public enum WindowTitleBarTheme { Default, Dark, Light }

    public class SilkNetWindowHost : Core.IWindowHost, IModalWindowService
    {
        private readonly string _title;
        private readonly int _width;
        private readonly int _height;
        private readonly Color4 _backgroundColor;

        private SilkNetSkiaWindow? _mainWindow;
        private SilkNetSkiaWindow? _activeModalWindow;
        private Action<int>? _onModalClosedCallback;
        private int _modalResultCode;
        private readonly Stopwatch _throttleTimer = new();
        private long _lastMainRepaintTicks;
        private static readonly long _modalRepaintIntervalTicks = Stopwatch.Frequency / 10;
        private bool _isDisposed;

        // Win32 fallback (only on Windows) for precise positioning
#if WINDOWS
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        private const uint SWP_NOSIZE     = 0x0001;
        private const uint SWP_NOZORDER   = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
#endif

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(
            IntPtr hWnd,
            int nIndex,
            IntPtr dwNewLong);
        private const int GWL_HWNDPARENT = -8;

        public AppEngine AppEngine => _mainWindow?.AppEngine ?? throw new InvalidOperationException("AppEngine is not initialized.");
        public IntPtr Handle => _mainWindow?.Handle ?? IntPtr.Zero;
        public InputManager Input => _mainWindow?.Input ?? new InputManager();
        public SizeI ClientSize => _mainWindow?.ClientSize ?? new SizeI(_width, _height);

        public bool ShowFpsCounter
        {
            get => _mainWindow?.ShowFpsCounter ?? false;
            set { if (_mainWindow is not null) _mainWindow.ShowFpsCounter = value; }
        }

        public IModalWindowService ModalWindowService => this;
        public bool IsModalWindowOpen => _activeModalWindow is not null;

        public WindowBackdropType BackdropType { get; set; } = WindowBackdropType.Default;
        public WindowTitleBarTheme TitleBarTheme { get; set; } = WindowTitleBarTheme.Dark;

        public SilkNetWindowHost(string title, int width, int height, Color4 backgroundColor)
        {
            _title = title;
            _width = width;
            _height = height;
            _backgroundColor = backgroundColor;
            _throttleTimer.Start();
        }

        public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor, float initialScale = 1.0f)
        {
            _mainWindow = new SilkNetSkiaWindow(
                _title, _width, _height, this, isModal: false);

            // Register fonts before initializing the window
            string fontPath = Path.Combine(AppContext.BaseDirectory, "Data", "Fonts", "Font Awesome 7 Free-Solid-900.otf");
            SilkNetTextService.RegisterFont("FontAwesomeSolid", fontPath);


            // Pass the initial scale down to the window, which will apply it when its AppEngine is created.
            return _mainWindow.Initialize(uiDrawCallback, backgroundColor, initialScale);
        }

        public void RunLoop()
        {
            if (_mainWindow is null)
                return;

            _mainWindow.IWindow.Initialize();
            _mainWindow.Render();
            _mainWindow.IWindow.IsVisible = true;
            _mainWindow.AppEngine.MarkDirty(); // Ensure it stays dirty for the first real loop iteration

            while (!_mainWindow.IWindow.IsClosing)
            {
                // 1. Process all events for all windows. Input handlers will mark the UI as dirty.
                _mainWindow.IWindow.DoEvents();
                _activeModalWindow?.IWindow.DoEvents();

                // 2. Check for other sources of change (like animations)
                if (!_isDisposed && _mainWindow.AppEngine.HasActiveAnimations) _mainWindow.AppEngine.MarkDirty();
                if (IsModalWindowOpen && _activeModalWindow?.AppEngine.HasActiveAnimations == true) _activeModalWindow.AppEngine.MarkDirty();

                // 3. Decide if rendering is needed
                bool mainDirty = !_isDisposed && _mainWindow.AppEngine.IsDirty;
                bool modalDirty = IsModalWindowOpen && (_activeModalWindow?.AppEngine.IsDirty ?? false);

                // Throttled repaint for the main window when a modal is active.
                bool repaintMainBehindModal = false;
                if (IsModalWindowOpen)
                {
                    var now = _throttleTimer.ElapsedTicks;
                    if (now - _lastMainRepaintTicks > _modalRepaintIntervalTicks)
                    {
                        repaintMainBehindModal = true;
                        _lastMainRepaintTicks = now;
                    }
                }

                if (mainDirty || modalDirty || repaintMainBehindModal)
                {
                    // Render main window if it's dirty, or if it's time for its throttled repaint.
                    if (mainDirty || repaintMainBehindModal)
                    {
                        _mainWindow.AppEngine.ClearDirtyFlag();
                        _mainWindow.Render();
                    }

                    // Handle modal window logic
                    if (_activeModalWindow is not null)
                    {
                        if (modalDirty)
                        {
                            _activeModalWindow.AppEngine.ClearDirtyFlag();
                            _activeModalWindow.Render();
                        }

                        if (_activeModalWindow.IWindow.IsClosing)
                        {
                            HandleModalClose();
                        }
                    }
                }
                else
                {
                    // Nothing to do, yield the CPU to reduce usage significantly.
                    // A 10ms sleep polls at ~100fps, which is responsive enough for latent changes.
                    Thread.Sleep(10);
                }
            }
        }

        public void OpenModalWindow(
            string title,
            int width,
            int height,
            Action<UIContext> drawCallback,
            Action<int>? onClosedCallback = null)
        {
            if (IsModalWindowOpen || _mainWindow is null)
                return;

            // Compute centered coords relative to parent
            var parentPos = _mainWindow.IWindow.Position;
            var parentSize = _mainWindow.IWindow.Size;
            var center = new Vector2D<int>(
                parentPos.X + (parentSize.X - width) / 2,
                parentPos.Y + (parentSize.Y - height) / 2
            );

            _onModalClosedCallback = onClosedCallback;
            _modalResultCode = -1;

            // Create the modal window hidden off‐screen
            var offscreen = new Vector2D<int>(-10000, -10000);
            _activeModalWindow = new SilkNetSkiaWindow(
                title, width, height, this, isModal: true, initialPosition: offscreen);

            if (!_activeModalWindow.Initialize(
                    drawCallback,
                    new Color4(60 / 255f, 60 / 255f, 60 / 255f, 1f)))
            {
                HandleModalClose();
                return;
            }

            // Force native handle creation (still hidden)
            _activeModalWindow.IWindow.Initialize();

            // Tell the OS about the owner relationship
            var modalHwnd = _activeModalWindow.Handle;
            var parentHwnd = Handle;
            if (modalHwnd != IntPtr.Zero && parentHwnd != IntPtr.Zero)
                SetWindowLongPtr(modalHwnd, GWL_HWNDPARENT, parentHwnd);

            // Cross‐platform reposition (Win32 fallback on Windows)
            if (modalHwnd != IntPtr.Zero)
            {
#if WINDOWS
                SetWindowPos(
                    modalHwnd,
                    IntPtr.Zero,
                    center.X, center.Y,
                    0, 0,
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
#else
                _activeModalWindow.IWindow.Position = center;
#endif
            }

            // Pre‐render the modal’s first frame off‐screen
            _activeModalWindow.Render();

            // Now reveal the modal with no blank flash
            _activeModalWindow.IWindow.IsVisible = true;

            // Disable and repaint the parent to avoid a blank‐frame
            if (parentHwnd != IntPtr.Zero)
            {
                EnableWindow(parentHwnd, false);
                _mainWindow.Render();
            }

            _lastMainRepaintTicks = _throttleTimer.ElapsedTicks;
        }

        public void CloseModalWindow(int resultCode = 0)
        {
            if (!IsModalWindowOpen)
                return;

            _modalResultCode = resultCode;
            _activeModalWindow?.IWindow.Close();
        }

        private void HandleModalClose()
        {
            var parentHwnd = Handle;

            // First, re-enable the parent window. The OS expects this during a modal teardown.
            if (parentHwnd != IntPtr.Zero)
            {
                EnableWindow(parentHwnd, true);
            }

            if (_activeModalWindow is not null)
            {
                // Now, before the OS can repaint, instantly move the modal far off-screen
                // and hide it. This is the crucial step to prevent the visual glitch.
                _activeModalWindow.IWindow.Position = new Vector2D<int>(-30000, -30000);
                _activeModalWindow.IWindow.IsVisible = false;
                _activeModalWindow.Dispose();
            }
            _activeModalWindow = null;

            // After the modal is gone, bring the parent window to the foreground.
            if (parentHwnd != IntPtr.Zero)
            {
                SetForegroundWindow(parentHwnd);
                _mainWindow?.IWindow.Focus();
            }

            Input.HardReset();
            _onModalClosedCallback?.Invoke(_modalResultCode);
            _onModalClosedCallback = null;
        }

        public void Cleanup()
        {
            if (_isDisposed) return;
            _activeModalWindow?.Dispose();
            _mainWindow?.Dispose();
            _isDisposed = true;
        }

        public void Dispose()
        {
            Cleanup();
            GC.SuppressFinalize(this);
        }
    }
}