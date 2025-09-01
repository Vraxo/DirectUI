using System;
using System.Collections.Generic;
using Vortice.Mathematics;

namespace DirectUI;

/// <summary>
/// Manages state that persists across frames, such as UI element instances
/// and input capture state (e.g., which element is currently being pressed).
/// </summary>
public class UIPersistentState
{
    // --- Persistent Element State ---
    private readonly Dictionary<int, object> uiElements = new();
    private readonly Dictionary<int, object?> _userData = new();

    // --- Popup/Overlay State ---
    private int _activePopupId;
    private Action<UIContext>? _popupDrawCallback;
    private Rect _popupBounds;
    public bool PopupWasOpenedThisFrame { get; private set; }


    // Staging area for results from the previous frame to be read in the current one.
    private int _nextFramePopupResult;
    private bool _nextFramePopupResultAvailable;
    private int _nextFramePopupResultOwnerId;

    public int PopupResult { get; private set; }
    public bool PopupResultAvailable { get; private set; }
    public int PopupResultOwnerId { get; private set; }


    public T GetOrCreateElement<T>(int id) where T : new()
    {
        if (uiElements.TryGetValue(id, out object? element) && element is T existingElement)
        {
            return existingElement;
        }

        T newElement = new();
        uiElements[id] = newElement;
        return newElement;
    }

    public void SetUserData(int id, object? data)
    {
        if (data is not null)
        {
            _userData[id] = data;
        }
        else
        {
            _userData.Remove(id);
        }
    }

    public object? GetUserData(int id)
    {
        return _userData.TryGetValue(id, out var data) ? data : null;
    }


    // --- Input State (persists across frames until interaction ends) ---
    public int ActivelyPressedElementId { get; private set; } = 0;
    public bool DragInProgressFromPreviousFrame { get; private set; } = false;
    public int FocusedElementId { get; private set; } = 0;

    // --- Input State (reset each frame) ---
    public int PotentialInputTargetId { get; private set; } = 0;
    public int InputCaptorId { get; private set; } = 0; // The element that captured the press for RELEASE mode.
    private int _inputCaptorLayer = -1; // The layer of the currently active press.
    public ClickCaptureServer ClickCaptureServer { get; } = new();

    // The winner of a 'Press' action, resolved at the end of the previous frame.
    public int PressActionWinnerId { get; private set; }
    private int _nextFramePressWinnerId;


    /// <summary>
    /// Resets the per-frame state variables. Called once at the beginning of each frame.
    /// </summary>
    public void ResetFrameState(InputState input)
    {
        ClickCaptureServer.Clear();
        DragInProgressFromPreviousFrame = input.IsLeftMouseDown && ActivelyPressedElementId != 0;
        PotentialInputTargetId = 0;

        // Reset the captor for visual "press" feedback if the mouse is not held down.
        if (!input.IsLeftMouseDown)
        {
            InputCaptorId = 0;
            _inputCaptorLayer = -1;
            ActivelyPressedElementId = 0;
        }

        // At the start of the frame, the winner from the *last* frame becomes the active winner for this frame.
        PressActionWinnerId = _nextFramePressWinnerId;
        _nextFramePressWinnerId = 0; // Clear the staging variable.

        // At the start of the frame, transfer the popup result from the previous frame
        // to the current frame's readable state.
        PopupResult = _nextFramePopupResult;
        PopupResultAvailable = _nextFramePopupResultAvailable;
        PopupResultOwnerId = _nextFramePopupResultOwnerId;

        // Clear the "next frame" state, making it ready for this frame's popups to write to.
        _nextFramePopupResult = 0;
        _nextFramePopupResultAvailable = false;
        _nextFramePopupResultOwnerId = 0;

        // Reset the popup-opened-this-frame flag.
        PopupWasOpenedThisFrame = false;
    }

    // --- Popup Management ---
    public bool IsPopupOpen => _activePopupId != 0;
    public int ActivePopupId => _activePopupId;
    public Rect PopupBounds => _popupBounds;
    public Action<UIContext>? PopupDrawCallback => _popupDrawCallback;

    public void SetActivePopup(int ownerId, Action<UIContext> drawCallback, Rect bounds)
    {
        _activePopupId = ownerId;
        _popupDrawCallback = drawCallback;
        _popupBounds = bounds;
        PopupWasOpenedThisFrame = true;
    }

    public void SetPopupResult(int ownerId, int result)
    {
        // This sets the data that will become available at the start of the *next* frame.
        _nextFramePopupResultOwnerId = ownerId;
        _nextFramePopupResult = result;
        _nextFramePopupResultAvailable = true;
    }

    public void ClearActivePopup()
    {
        _activePopupId = 0;
        _popupDrawCallback = null;
        _popupBounds = default;
    }


    // --- Input Capture && Targeting ---
    public bool IsElementActive()
    {
        return ActivelyPressedElementId != 0;
    }

    public void SetPotentialInputTarget(int id)
    {
        // Don't allow elements to become potential targets if a popup is open and the cursor is outside it.
        if (IsPopupOpen && !_popupBounds.Contains(UI.Context.InputState.MousePosition))
        {
            return;
        }

        // The last widget drawn under the mouse is the topmost one, so it should always
        // be allowed to set itself as the potential target, overwriting any widget drawn beneath it.
        PotentialInputTargetId = id;
    }

    // Used for immediate visual feedback (which element is currently held down)
    public bool TrySetActivePress(int id, int layer)
    {
        if (layer >= _inputCaptorLayer)
        {
            _inputCaptorLayer = layer;
            InputCaptorId = id; // This element is the captor for release checks
            ActivelyPressedElementId = id; // This element is visually pressed
            return true;
        }
        return false;
    }

    // Called at the end of the frame to set the winner for the next frame's Press actions
    public void SetNextFramePressWinner(int id)
    {
        _nextFramePressWinnerId = id;
    }

    public void ClearActivePress(int id)
    {
        if (ActivelyPressedElementId == id)
        {
            ActivelyPressedElementId = 0;
        }
    }

    // --- Focus Management ---
    public void SetFocus(int id)
    {
        FocusedElementId = id;
    }
}