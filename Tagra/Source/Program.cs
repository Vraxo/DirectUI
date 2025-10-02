using DirectUI;
using DirectUI.Core;
using Tagra;

// Run the Tagra application using the Direct2D backend.
// The ApplicationRunner will create a window host and pass it to our App logic.
ApplicationRunner.Run(GraphicsBackend.SkiaSharp, (host) => new App(host));