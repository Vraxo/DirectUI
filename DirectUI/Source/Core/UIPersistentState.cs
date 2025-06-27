using System;
using System.Collections.Generic;

namespace DirectUI;

/// <summary>
/// Manages state that persists across frames, such as UI element instances
/// and input capture state (e.g., which element is currently being pressed).
/// </summary>
public class UIPersistentState
{
    // --- Persistent Element State ---
    private readonly Dictionary<string, object> uiElements = new();

    public T GetOrCreateElement<T>(string id) where T : new()
    {
        if (uiElements.TryGetValue(id, out object? element) && element is T existingElement)
        {
            return existingElement;
        }

        T newElement = new();
        uiElements[id] = newElement;
        return newElement;
    }


    // --- Input State (persists across frames until interaction ends) ---
    public int ActivelyPressedElementId { get; private set; } = 0;
    public bool DragInProgressFromPreviousFrame { get; private set; } = false;

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
    }

    // --- Input Capture & Targeting ---
    public bool IsElementActive()
    {
        return ActivelyPressedElementId != 0;
    }

    public void SetPotentialInputTarget(int id)
    {
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
}