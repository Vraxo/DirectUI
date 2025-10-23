// Entire file content here
using System;
using DirectUI;
using DirectUI.Core;

namespace CherrisEditor
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Use STAThread for Win32 backend compatibility
            ApplicationRunner.Run(GraphicsBackend.SkiaSharp,
                (host) => new EditorAppLogic(host));
        }
    }
}