using Apexverse;
using Cherris;
using System;

namespace CherrisEditor
{
    public class EditorGame : Apexverse.Game
    {
        private System.Diagnostics.Stopwatch _stopwatch = new();
        private bool _initialized = false;

        // This now calls the new protected constructor on Apexverse.Game,
        // which calls the headless constructor on Cherris.Engine.
        public EditorGame() : base(EngineMode.Editor)
        {
        }

        public void InitializeForEditor(IntPtr handle, int width, int height)
        {
            base.InitializeForEditor(handle, width, height);

            base.LoadContent();
            base.Start(); // This is the crucial fix that was missing
            base.OnStart();

            _initialized = true;
            _stopwatch.Start();
        }

        public void Tick()
        {
            if (!_initialized) return;

            // In editor mode, input events are still pumped by the host (WinForms)
            // but we need to update our static Input class state.
            // For simplicity, we assume the host does this. If not, we'd call a method here.

            if (!base.GameWindow.Exists)
            {
                return;
            }

            float deltaTime = (float)_stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();

            base.Update(deltaTime);
            base.DrawFrame();
        }
    }
}