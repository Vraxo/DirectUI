# State Corruption in Multi-Pass Immediate-Mode UI

## 1. State Corruption with Dynamic Layout Widgets

**Symptom:**
A widget that changes state, like a `ColorSelector`, is placed inside a container that dynamically sizes itself based on its content, like a `UI.AutoPanel`. When the user interacts with the widget (e.g., clicks a new color), the action is detected but the UI state does not update correctly. The click seems to have no effect. Debugging shows the click event fires, but the state change appears to be lost or reverted almost immediately.

**Cause:**
This complex issue stems from how the `UI.AutoPanel` container works. To determine its own size, it must run the UI code for its contents **twice** within a single frame:

1. **Calculation Pass:** The UI logic is executed with a "null renderer." This pass processes input and calculates the layout of all child widgets to measure their total size, but does not draw anything.
2. **Drawing Pass:** After the `AutoPanel` knows its final size, it executes the same UI logic a second time, this time with the real renderer to actually draw the widgets to the screen.

The bug occurs when the **Calculation Pass** causes a side-effect that corrupts the state needed for the **Drawing Pass**.

---

### **Hypothesis #1 (Incorrect): Deferred Action Failure**

* **Diagnosis:** The state change (database update, data refresh) was happening inside the `if` block during the Calculation Pass. This was thought to be corrupting the `AllTags` list that the `foreach` loop was iterating over, causing the Drawing Pass to fail.
* **Attempted Fix:** Defer the state-changing logic. A flag or an `Action` was set during the UI code, and the actual database update was invoked *after* the `AutoPanel` widget had finished both its passes.
* **Result:** Still failed. The UI interaction itself was being lost, not just the resulting action.

---

### **Hypothesis #2 (Incorrect): `ref` vs `out` Parameter Issue**

* **Diagnosis:** The `ColorSelector` used a `ref` parameter. It was thought that the Calculation Pass was modifying this `ref` parameter. Then, when the Drawing Pass started, it saw the already-modified value, concluded no change occurred, and returned `false`.
* **Attempted Fix:** Refactor `ColorSelector` to remove the `ref` parameter and instead use an `out` parameter to report the change. This would prevent the input state from being modified between passes.
* **Result:** Still failed. While this was good practice for preventing side-effects, it did not address the core issue. The click event itself was still being consumed prematurely.

---

### **Final Diagnosis (Correct): Input State Annihilation**

The true cause was that the Calculation Pass was processing the *entire* input-handling pipeline.

1. During the **Calculation Pass**, `DrawButtonPrimitive` correctly detects the mouse click.
2. As part of a successful click, it calls `state.ClearActivePress()`. This **resets the global `UIPersistentState`**, clearing which UI element is considered "pressed".
3. The Calculation Pass finishes.
4. The **Drawing Pass** begins.
5. `DrawButtonPrimitive` runs again for the same button. It processes the same mouse input, but because `state.ActivelyPressedElementId` was already cleared, it no longer detects a valid click release.

The first pass consumed and then erased the very input state the second pass needed to confirm the action.

**Solution:**
The Calculation Pass must be made truly **read-only** with respect to any persistent, frame-to-frame UI state. It should be able to read input for layout purposes (like hover effects) but must be prohibited from modifying any state that persists beyond its own execution.

**How to Fix:**

1. **Introduce a "Layout Pass" Flag:** A boolean flag, `IsLayoutPass`, was added to the `UIContext`.
2. **Activate Flag During Measurement:** The `UI.CalculateLayout` method (used by `AutoPanel`) was modified to set `Context.IsLayoutPass = true` before executing the user's UI code in its Calculation Pass, and to reset it in a `finally` block.
3. **Guard State Mutations:** All methods in `UIPersistentState` and `ClickCaptureServer` that modify state (e.g., `SetFocus`, `ClearActivePress`, `RegisterClick`, `RequestCapture`) were guarded with an initial check: `if (UI.Context.IsLayoutPass) return;`.

This ensures that during the Calculation Pass, the UI framework simply ignores any requests to change its core state, preserving it in a pristine condition for the final Drawing Pass where the user's input can be processed correctly and definitively.
