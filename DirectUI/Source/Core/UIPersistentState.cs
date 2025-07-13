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
    public int InputCaptorId { get; private set; } = 0;
    private bool captureAttemptedThisFrame = false;
    public bool NonSliderElementClaimedPress { get; private set; } = false;


    /// <summary>
    /// Resets the per-frame state variables. Called once at the beginning of each frame.
    /// </summary>
    public void ResetFrameState(InputState input)
    {
        DragInProgressFromPreviousFrame = input.IsLeftMouseDown && ActivelyPressedElementId != 0;
        PotentialInputTargetId = 0;
        InputCaptorId = 0;
        captureAttemptedThisFrame = false;
        NonSliderElementClaimedPress = false;

        // At the start of the frame, transfer the popup result from the previous frame
        // to the current frame's readable state.
        PopupResult = _nextFramePopupResult;
        PopupResultAvailable = _nextFramePopupResultAvailable;
        PopupResultOwnerId = _nextFramePopupResultOwnerId;

        // Clear the "next frame" state, making it ready for this frame's popups to write to.
        _nextFramePopupResult = 0;
        _nextFramePopupResultAvailable = false;
        _nextFramePopupResultOwnerId = 0;
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
        PotentialInputTargetId = id;
    }

    public void SetPotentialCaptorForFrame(int id)
    {
        captureAttemptedThisFrame = true;
        InputCaptorId = id;
        ActivelyPressedElementId = id;
    }

    public void SetButtonPotentialCaptorForFrame(int id)
    {
        captureAttemptedThisFrame = true;
        InputCaptorId = id;
        ActivelyPressedElementId = id;
        NonSliderElementClaimedPress = true;
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