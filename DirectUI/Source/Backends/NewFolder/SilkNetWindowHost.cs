using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DirectUI.Core;
using DirectUI.Input;
using Silk.NET.Maths;
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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        private const int GWL_HWNDPARENT = -8;

        public IntPtr Handle => _mainWindow?.Handle ?? IntPtr.Zero;
        public InputManager Input => _mainWindow?.Input ?? new InputManager();
        public SizeI ClientSize => _mainWindow?.ClientSize ?? new SizeI(_width, _height);
        public bool ShowFpsCounter
        {
            get => _mainWindow?.ShowFpsCounter ?? false;
            set { if (_mainWindow != null) _mainWindow.ShowFpsCounter = value; }
        }
        public IModalWindowService ModalWindowService => this;
        public bool IsModalWindowOpen => _activeModalWindow != null;

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

        public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
        {
            _mainWindow = new SilkNetSkiaWindow(_title, _width, _height, this, isModal: false);
            return _mainWindow.Initialize(uiDrawCallback, backgroundColor);
        }

        public void RunLoop()
        {
            if (_mainWindow == null) return;

            _mainWindow.IWindow.Initialize();
            _mainWindow.IWindow.IsVisible = true;

            while (!_mainWindow.IWindow.IsClosing)
            {
                _mainWindow.IWindow.DoEvents();

                bool renderMain = !IsModalWindowOpen;
                if (IsModalWindowOpen)
                {
                    long now = _throttleTimer.ElapsedTicks;
                    if (now - _lastMainRepaintTicks > _modalRepaintIntervalTicks)
                    {
                        renderMain = true;
                        _lastMainRepaintTicks = now;
                    }
                }

                if (renderMain)
                {
                    _mainWindow.Render();
                }

                if (IsModalWindowOpen && !_activeModalWindow!.IWindow.IsClosing)
                {
                    _activeModalWindow.Render();
                }

                if (IsModalWindowOpen && _activeModalWindow!.IWindow.IsClosing)
                {
                    HandleModalClose();
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
            if (IsModalWindowOpen || _mainWindow == null) return;

            // 1. Compute centered position relative to parent
            Vector2D<int>? centered = null;
            if (_mainWindow.IWindow != null)
            {
                var p = _mainWindow.IWindow.Position;
                var s = _mainWindow.IWindow.Size;
                centered = new Vector2D<int>(
                    p.X + (s.X - width) / 2,
                    p.Y + (s.Y - height) / 2);
            }

            _onModalClosedCallback = onClosedCallback;
            _modalResultCode = -1;

            // 2. Create positioned modal
            _activeModalWindow = new SilkNetSkiaWindow(
                title, width, height, this, isModal: true, initialPosition: centered);

            if (!_activeModalWindow.Initialize(drawCallback, new Color4(60 / 255f, 60 / 255f, 60 / 255f, 1.0f)))
            {
                HandleModalClose();
                return;
            }

            // 3. Initialize (creates native window hidden)
            _activeModalWindow.IWindow.Initialize();

            // 4. Set owner window so OS knows the relation
            var modalHwnd = _activeModalWindow.Handle;
            var parentHwnd = Handle;
            if (modalHwnd != IntPtr.Zero && parentHwnd != IntPtr.Zero)
            {
                SetWindowLongPtr(modalHwnd, GWL_HWNDPARENT, parentHwnd);
            }

            // 5. Make modal visible
            _activeModalWindow.IWindow.IsVisible = true;

            // 6. Disable parent and immediately repaint it
            if (parentHwnd != IntPtr.Zero)
            {
                EnableWindow(parentHwnd, false);
                _mainWindow.Render();
            }

            // Reset throttle so we don't skip the very next parent repaint
            _lastMainRepaintTicks = _throttleTimer.ElapsedTicks;
        }

        public void CloseModalWindow(int resultCode = 0)
        {
            if (!IsModalWindowOpen) return;
            _modalResultCode = resultCode;
            _activeModalWindow?.IWindow.Close();
        }

        private void HandleModalClose()
        {
            var parentHwnd = Handle;

            _activeModalWindow?.Dispose();
            _activeModalWindow = null;

            if (parentHwnd != IntPtr.Zero)
            {
                EnableWindow(parentHwnd, true);
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
