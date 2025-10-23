// Entire file content here
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using Apexverse;
using Cherris;
using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;
using Vortice.Mathematics;
using MouseButton = Cherris.MouseButton;

namespace CherrisEditor
{
    public class EditorAppLogic : IAppLogic, IDisposable
    {
        private readonly IWindowHost _host;
        private readonly EditorGame _game;
        private readonly System.Threading.Timer _gameLoopTimer;

        private byte[]? _lastFrameBytes;
        private readonly object _frameLock = new();

        private float _hierarchyWidth = 250f;
        private float _inspectorWidth = 300f;
        private float _consoleHeight = 200f;
        private const float _menuBarHeight = 30f; // Placeholder for a future menu bar

        private bool _isViewportActive = false;

        public EditorAppLogic(IWindowHost host)
        {
            _host = host;
            _game = new EditorGame();

            // Use the handle from the DirectUI host window to initialize the engine.
            // This removes the dependency on the hidden WinForms window.
            _game.InitializeForEditor(_host.Handle, 1280, 720); // Initial internal resolution

            _gameLoopTimer = new System.Threading.Timer(GameLoopTick, null, 0, 16); // ~60 FPS
        }

        private void GameLoopTick(object? state)
        {
            if (_game.GameWindow?.Exists != true) return;

            // Update Cherris engine input state and tick the game
            Input.FrameStarted();
            _game.Tick();

            // Signal the renderer to capture the frame it just rendered
            _game.RequestFrameCapture();

            // Retrieve the captured frame. This causes a GPU-CPU sync.
            var frame = _game.GetLastCapturedFrameAsBmp();
            lock (_frameLock)
            {
                _lastFrameBytes = frame;
            }
        }

        public void DrawUI(UIContext context)
        {
            // Draw Panels
            UI.BeginResizableVPanel("hierarchy_panel", ref _hierarchyWidth, HAlignment.Left, topOffset: _menuBarHeight, minWidth: 150, maxWidth: 500);
            UI.Text("hierarchy_label", "Scene Hierarchy");
            UI.EndResizableVPanel();

            UI.BeginResizableVPanel("inspector_panel", ref _inspectorWidth, HAlignment.Right, topOffset: _menuBarHeight, minWidth: 200, maxWidth: 600);
            UI.Text("inspector_label", "Inspector");
            UI.EndResizableVPanel();

            UI.BeginResizableHPanel("console_panel", ref _consoleHeight, reservedLeftSpace: _hierarchyWidth, reservedRightSpace: _inspectorWidth, minHeight: 40, maxHeight: 400);
            UI.Text("console_label", "Console");
            UI.EndResizableHPanel();

            // Draw Viewport in the remaining area
            DrawViewport(context);
        }

        private void DrawViewport(UIContext context)
        {
            var renderSize = context.Renderer.RenderTargetSize;

            float x = _hierarchyWidth;
            float y = _menuBarHeight;
            float width = renderSize.X - _hierarchyWidth - _inspectorWidth;
            float height = renderSize.Y - _menuBarHeight - _consoleHeight;

            if (width < 1 || height < 1) return;

            var viewportLogicalPos = new Vector2(x, y);
            var viewportLogicalSize = new Vector2(width, height);

            // DirectUI works with physical pixels, so we don't need to scale here.
            var viewportBounds = new Rect(x, y, width, height);

            // Draw background and image
            UI.BeginVBoxContainer("viewport_vbox", viewportLogicalPos);
            {
                UI.Box("viewport_bg", viewportLogicalSize, new BoxStyle { FillColor = new DirectUI.Drawing.Color(20, 20, 20, 255), Roundness = 0f });

                byte[]? frameToDraw = null;
                lock (_frameLock)
                {
                    frameToDraw = _lastFrameBytes;
                }

                if (frameToDraw != null)
                {
                    // The UI.Image needs to be placed at the top-left of the container, which is already at viewportLogicalPos.
                    // We achieve this by resetting the layout cursor inside the container if needed, but since it's the first
                    // element, it will be placed at the container's start position by default.
                    UI.Image("viewport_image", frameToDraw, viewportLogicalSize);
                }
            }
            UI.EndVBoxContainer(advanceParentLayout: false); // We are manually managing layout here.

            // Handle Input Forwarding
            HandleViewportInput(context.InputState, viewportBounds);
        }

        private void HandleViewportInput(InputState duiInput, Rect viewportBounds)
        {
            var relativeMousePos = duiInput.MousePosition - new Vector2(viewportBounds.X, viewportBounds.Y);
            bool isHovered = viewportBounds.Contains(duiInput.MousePosition);

            // Forward mouse position relative to the viewport
            Input.SetMousePosition(relativeMousePos);

            // Free-look camera with Right Mouse Button
            if (isHovered && duiInput.WasRightMousePressedThisFrame)
            {
                _isViewportActive = true;
                Input.IsMouseLocked = true;
            }

            if (_isViewportActive)
            {
                // Forward mouse delta for camera rotation
                var delta = duiInput.MousePosition - duiInput.PreviousMousePosition;
                if (delta.LengthSquared() > 0)
                {
                    Input.AddMouseDelta(delta);
                }

                // Forward movement keys
                foreach (var key in duiInput.PressedKeys) Input.SetKeyState(InputMapper.MapDirectUIKey(key), true);
                foreach (var key in duiInput.ReleasedKeys) Input.SetKeyState(InputMapper.MapDirectUIKey(key), false);
            }

            if (!duiInput.IsRightMouseDown)
            {
                _isViewportActive = false;
                Input.IsMouseLocked = false;
                Input.SetKeyState(Key.W, false);
                Input.SetKeyState(Key.A, false);
                Input.SetKeyState(Key.S, false);
                Input.SetKeyState(Key.D, false);
            }

            // Forward mouse button clicks (for object picking, etc.)
            if (isHovered)
            {
                if (duiInput.WasLeftMousePressedThisFrame) Input.SetMouseButtonState(MouseButton.Left, true);
            }
            // Always forward mouse up events regardless of hover to avoid stuck states
            if (!duiInput.IsLeftMouseDown && Input.IsMouseButtonDown(MouseButton.Left)) Input.SetMouseButtonState(MouseButton.Left, false);
        }

        public void SaveState()
        {
            // Placeholder for saving editor state (e.g., panel sizes)
        }

        public void Dispose()
        {
            _gameLoopTimer?.Dispose();
            _game?.GameWindow?.Dispose();
        }
    }
}