// DirectUI/Core/IModalWindowService.cs
using System;
using Vortice.Mathematics;

namespace DirectUI.Core;

/// <summary>
/// Defines the contract for a service that can open modal windows.
/// This allows application UI logic to request a modal window without
/// knowing the underlying platform's windowing specifics.
/// </summary>
public interface IModalWindowService
{
    /// <summary>
    /// Opens a modal window with the specified properties.
    /// </summary>
    /// <param name="title">The title of the modal window.</param>
    /// <param name="width">The width of the modal window.</param>
    /// <param name="height">The height of the modal window.</param>
    /// <param name="drawCallback">The UI drawing logic for the content of the modal window.</param>
    /// <param name="onClosedCallback">An action to be invoked when the modal window is closed. Receives a result code (0 for success, non-zero for error/cancel).</param>
    void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null);

    /// <summary>
    /// Closes the currently active modal window, if any, with a specified result code.
    /// </summary>
    /// <param name="resultCode">A code indicating the outcome of the modal interaction (e.g., 0 for OK, 1 for Cancel).</param>
    void CloseModalWindow(int resultCode = 0);

    /// <summary>
    /// Checks if a modal window is currently open.
    /// </summary>
    bool IsModalWindowOpen { get; }
}
