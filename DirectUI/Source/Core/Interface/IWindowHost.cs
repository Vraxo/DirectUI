// DirectUI/Core/IWindowHost.cs
using System;
using DirectUI.Input;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI.Core;

/// <summary>
/// Defines the contract for a platform-specific window host that integrates with the DirectUI engine.
/// This interface abstracts window creation, main loop management, and exposes input and modal services.
/// </summary>
public interface IWindowHost : IDisposable
{
    /// <summary>
    /// Gets the native window handle, if applicable. Returns IntPtr.Zero otherwise.
    /// </summary>
    IntPtr Handle { get; }

    /// <summary>
    /// Gets the AppEngine instance associated with this host.
    /// </summary>
    AppEngine AppEngine { get; }

    /// <summary>
    /// Provides access to the InputManager for the host to feed input events.
    /// </summary>
    InputManager Input { get; }

    /// <summary>
    /// Gets the current client size of the window.
    /// </summary>
    SizeI ClientSize { get; }

    /// <summary>
    /// Gets or sets whether the FPS counter should be displayed.
    /// </summary>
    bool ShowFpsCounter { get; set; }

    /// <summary>
    /// Provides access to a service for opening modal windows specific to this host's platform.
    /// </summary>
    IModalWindowService ModalWindowService { get; }

    /// <summary>
    /// Initializes the window host and the DirectUI engine.
    /// </summary>
    /// <param name="uiDrawCallback">The callback function that contains the application's UI drawing logic.</param>
    /// <param name="backgroundColor">The background color to clear the window with each frame.</param>
    /// <param name="initialScale">The initial UI scale factor to apply.</param>
    /// <returns>True if initialization was successful, false otherwise.</returns>
    bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor, float initialScale = 1.0f);

    /// <summary>
    /// Runs the platform-specific main message/event loop for the application.
    /// </summary>
    void RunLoop();

    /// <summary>
    /// Cleans up resources owned by the window host.
    /// </summary>
    void Cleanup();
}