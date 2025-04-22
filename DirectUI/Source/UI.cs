// MODIFIED: UI.cs
// Summary: Providing the full class implementation including previously abbreviated methods.
using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

// Enums (ensure they exist)
public enum HSliderDirection { LeftToRight, RightToLeft }
public enum VSliderDirection { TopToBottom, BottomToTop }


public static class UI
{
    private static ID2D1HwndRenderTarget? currentRenderTarget;
    private static IDWriteFactory? currentDWriteFactory;
    private static InputState currentInputState;
    private static readonly Dictionary<string, object> uiElements = new(); // Stores Buttons, Sliders etc.
    private static readonly Dictionary<Color4, ID2D1SolidColorBrush> brushCache = new();
    private static readonly Stack<object> containerStack = new();

    public static ID2D1HwndRenderTarget? CurrentRenderTarget => currentRenderTarget;
    public static IDWriteFactory? CurrentDWriteFactory => currentDWriteFactory;
    public static InputState CurrentInputState => currentInputState;

    public static void BeginFrame(DrawingContext context, InputState input)
    {
        currentRenderTarget = context.RenderTarget;
        currentDWriteFactory = context.DWriteFactory;
        currentInputState = input;
        containerStack.Clear();
    }

    public static void EndFrame()
    {
        if (containerStack.Count > 0)
        {
            Console.WriteLine($"Warning: Mismatch in Begin/End container calls. {containerStack.Count} containers left open.");
            containerStack.Clear(); // Clear on error
        }
        currentRenderTarget = null;
        currentDWriteFactory = null;
    }

    // --- HBox Container ---
    public static void BeginHBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Vector2 startPosition = ApplyLayout(position); // Get starting pos based on parent
        var containerState = new HBoxContainerState(id, startPosition, gap);
        containerStack.Push(containerState);
    }
    public static void EndHBoxContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not HBoxContainerState state)
        {
            Console.WriteLine("Error: EndHBoxContainer mismatch."); return;
        }
        containerStack.Pop(); // Remove self
        if (IsInLayoutContainer()) // Advance parent if nested
        {
            Vector2 containerSize = new Vector2(state.AccumulatedWidth, state.MaxElementHeight);
            AdvanceLayoutCursor(containerSize);
        }
    }

    // --- VBox Container ---
    public static void BeginVBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Vector2 startPosition = ApplyLayout(position);
        var containerState = new VBoxContainerState(id, startPosition, gap);
        containerStack.Push(containerState);
    }
    public static void EndVBoxContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not VBoxContainerState state)
        {
            Console.WriteLine("Error: EndVBoxContainer mismatch."); return;
        }
        containerStack.Pop();
        if (IsInLayoutContainer())
        {
            Vector2 containerSize = new Vector2(state.MaxElementWidth, state.AccumulatedHeight);
            AdvanceLayoutCursor(containerSize);
        }
    }

    // --- Grid Container ---
    public static void BeginGridContainer(string id, Vector2 position, Vector2 availableSize, int numColumns, Vector2 gap)
    {
        Vector2 startPosition = ApplyLayout(position); // Position relative to parent container or screen
        var containerState = new GridContainerState(id, startPosition, availableSize, numColumns, gap);
        containerStack.Push(containerState);
    }

    public static void EndGridContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not GridContainerState state)
        {
            Console.WriteLine("Error: EndGridContainer mismatch."); return;
        }
        containerStack.Pop();
        if (IsInLayoutContainer())
        {
            // Finalize height calculation for the last row before getting total size
            // Check if RowHeights has any elements before accessing last element
            if (state.RowHeights.Count > 0)
            {
                state.RowHeights[^1] = state.CurrentRowMaxHeight;
                // Add the height of the (now finished) last row to accumulated height
                state.AccumulatedHeight += state.CurrentRowMaxHeight;
                // Add gap if it wasn't the very first row
                if (state.RowHeights.Count > 1) // If more than one row was processed (Count includes the final row)
                {
                    state.AccumulatedHeight += state.Gap.Y;
                }
            }


            Vector2 containerSize = state.GetTotalOccupiedSize(); // Get size based on content
            AdvanceLayoutCursor(containerSize);
        }
    }

    // --- Widgets (Button, Sliders) ---
    public static bool Button(string id, ButtonDefinition definition)
    {
        if (!IsContextValid() || definition is null) return false;
        Button buttonInstance = GetOrCreateElement<Button>(id);
        Vector2 elementPosition = GetCurrentLayoutPositionInternal(); // Get position from current layout state
        buttonInstance.Position = elementPosition;
        ApplyButtonDefinition(buttonInstance, definition);
        buttonInstance.UpdateStyle();
        if (CurrentDWriteFactory is not null) buttonInstance.PerformAutoWidth(CurrentDWriteFactory);
        bool clicked = buttonInstance.Update();
        AdvanceLayout(buttonInstance.Size); // Tell layout manager the size used
        return clicked;
    }
    public static float HSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;
        InternalHSliderLogic sliderInstance = GetOrCreateElement<InternalHSliderLogic>(id);
        Vector2 elementPosition = GetCurrentLayoutPositionInternal();
        sliderInstance.Position = elementPosition;
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.HorizontalDirection;
        float newValue = sliderInstance.UpdateAndDraw(CurrentInputState, GetCurrentDrawingContext(), currentValue);
        AdvanceLayout(sliderInstance.Size);
        return newValue;
    }
    public static float VSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;
        InternalVSliderLogic sliderInstance = GetOrCreateElement<InternalVSliderLogic>(id);
        Vector2 elementPosition = GetCurrentLayoutPositionInternal();
        sliderInstance.Position = elementPosition;
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.VerticalDirection;
        float newValue = sliderInstance.UpdateAndDraw(CurrentInputState, GetCurrentDrawingContext(), currentValue);
        AdvanceLayout(sliderInstance.Size);
        return newValue;
    }


    // --- Helper Methods ---
    private static bool IsContextValid()
    {
        if (CurrentRenderTarget is null || CurrentDWriteFactory is null)
        {
            Console.WriteLine($"Error: UI method called outside BeginFrame/EndFrame or context is invalid.");
            return false;
        }
        return true;
    }
    private static DrawingContext GetCurrentDrawingContext()
    {
        // Assumes context is valid (checked by IsContextValid)
        return new DrawingContext(CurrentRenderTarget!, CurrentDWriteFactory!);
    }


    private static T GetOrCreateElement<T>(string id) where T : new()
    {
        if (uiElements.TryGetValue(id, out object? element) && element is T existingElement)
        {
            return existingElement;
        }
        else
        {
            // Console.WriteLine($"Creating new {typeof(T).Name} instance for ID: {id}"); // Optional logging
            T newElement = new();
            uiElements[id] = newElement;
            return newElement;
        }
    }

    private static void ApplyButtonDefinition(Button instance, ButtonDefinition definition)
    {
        // Size must be applied before potential AutoWidth calculation
        instance.Size = definition.Size;
        instance.Text = definition.Text;
        instance.Themes = definition.Theme ?? instance.Themes ?? new ButtonStylePack(); // Ensure theme exists
        instance.Origin = definition.Origin ?? Vector2.Zero;
        instance.TextAlignment = definition.TextAlignment ?? new Alignment(HAlignment.Center, VAlignment.Center);
        instance.TextOffset = definition.TextOffset ?? Vector2.Zero;
        instance.AutoWidth = definition.AutoWidth;
        instance.TextMargin = definition.TextMargin ?? new Vector2(10, 5);
        instance.Behavior = definition.Behavior;
        instance.LeftClickActionMode = definition.LeftClickActionMode;
        instance.Disabled = definition.Disabled;
        instance.UserData = definition.UserData;
    }

    private static void ApplySliderDefinition(InternalSliderLogic instance, SliderDefinition definition)
    {
        instance.Size = definition.Size; // Apply size first
        instance.MinValue = definition.MinValue;
        instance.MaxValue = definition.MaxValue;
        instance.Step = definition.Step;
        instance.Theme = definition.Theme ?? instance.Theme ?? new SliderStyle(); // Ensure theme exists
        instance.GrabberTheme = definition.GrabberTheme ?? instance.GrabberTheme ?? new ButtonStylePack(); // Ensure grabber theme exists
        instance.GrabberSize = definition.GrabberSize ?? instance.GrabberSize; // Use existing if null
        instance.Origin = definition.Origin ?? Vector2.Zero;
        instance.Disabled = definition.Disabled;
        instance.UserData = definition.UserData;
        // Note: Position is set via layout system
        // Note: Direction is set specifically in HSlider/VSlider methods
    }


    // --- Layout Helpers ---
    private static Vector2 ApplyLayout(Vector2 defaultPosition)
    {
        // If inside a container, return the container's next position, otherwise return default.
        return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : defaultPosition;
    }
    private static void AdvanceLayout(Vector2 elementSize)
    {
        // If inside a container, advance its cursor.
        if (IsInLayoutContainer())
        {
            AdvanceLayoutCursor(elementSize);
        }
    }
    private static bool IsInLayoutContainer() => containerStack.Count > 0;

    // Renamed internal version called by ApplyLayout and widgets
    private static Vector2 GetCurrentLayoutPositionInternal()
    {
        if (!IsInLayoutContainer()) return Vector2.Zero; // Should not happen if called correctly
        object currentContainerState = containerStack.Peek();
        return currentContainerState switch
        {
            HBoxContainerState hbox => hbox.CurrentPosition,
            VBoxContainerState vbox => vbox.CurrentPosition,
            GridContainerState grid => grid.CurrentDrawPosition, // Use the grid's calculated position
            _ => Vector2.Zero, // Error case
        };
    }

    // Public version for external use if needed (e.g. drawing labels)
    public static Vector2 GetCurrentLayoutPosition()
    {
        return GetCurrentLayoutPositionInternal();
    }


    private static void AdvanceLayoutCursor(Vector2 elementSize)
    {
        // No null check needed, IsInLayoutContainer was checked by caller
        object currentContainerState = containerStack.Peek();

        switch (currentContainerState)
        {
            case HBoxContainerState hbox:
                if (elementSize.Y > hbox.MaxElementHeight) hbox.MaxElementHeight = elementSize.Y;
                // Add gap BEFORE the element if it's not the first one
                float advanceX = (hbox.AccumulatedWidth > 0 ? hbox.Gap : 0) + elementSize.X;
                hbox.CurrentPosition = new Vector2(hbox.CurrentPosition.X + advanceX, hbox.CurrentPosition.Y);
                // Recalculate accumulated width based on the new edge position relative to start
                hbox.AccumulatedWidth = hbox.CurrentPosition.X - hbox.StartPosition.X;
                break;

            case VBoxContainerState vbox:
                if (elementSize.X > vbox.MaxElementWidth) vbox.MaxElementWidth = elementSize.X;
                // Add gap BEFORE the element if it's not the first one
                float advanceY = (vbox.AccumulatedHeight > 0 ? vbox.Gap : 0) + elementSize.Y;
                vbox.CurrentPosition = new Vector2(vbox.CurrentPosition.X, vbox.CurrentPosition.Y + advanceY);
                // Recalculate accumulated height based on the new edge position relative to start
                vbox.AccumulatedHeight = vbox.CurrentPosition.Y - vbox.StartPosition.Y;
                break;

            case GridContainerState grid:
                grid.MoveToNextCell(elementSize); // Grid state handles its own cursor movement
                break;
        }
    }
    // --- End Layout Helpers ---


    public static ID2D1SolidColorBrush GetOrCreateBrush(Color4 color)
    {
        // This method assumes IsContextValid() was checked by the caller (e.g., Button, Slider methods)
        // or that it's called in a context where the render target is known to be valid.
        if (CurrentRenderTarget is null)
        {
            Console.WriteLine("Error: GetOrCreateBrush called with no active render target (should have been checked earlier).");
            // Returning null will likely cause drawing errors downstream. Consider throwing an exception
            // if this state is truly unexpected.
            return null!;
        }

        // Check cache first
        if (brushCache.TryGetValue(color, out ID2D1SolidColorBrush? brush) && brush is not null)
        {
            // Optional: Add a check if the brush's render target matches the current one,
            // although CleanupResources should handle this if RT recreation is managed correctly.
            // if (brush.RenderTarget == CurrentRenderTarget) // Pseudocode, direct RT access isn't typical
            // {
            return brush;
            // }
            // else { /* Stale brush, remove from cache */ brushCache.Remove(color); }
        }
        else if (brush is null && brushCache.ContainsKey(color))
        {
            // Entry exists but brush is null (likely disposed previously), remove the key.
            brushCache.Remove(color);
        }

        // Brush not in cache or was stale/null, try to create it.
        try
        {
            brush = CurrentRenderTarget.CreateSolidColorBrush(color);
            if (brush is not null)
            {
                brushCache[color] = brush; // Add the newly created brush to cache.
                return brush;
            }
            else
            {
                // This case should be rare but handle it defensively.
                Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}");
                return null!; // Indicate failure to create brush.
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            // RenderTarget is lost, cannot create brush.
            Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). External cleanup needed.");
            // The application's main loop (Direct2DAppWindow) should detect RecreateTarget
            // and call CleanupResources, which will clear the cache.
            return null!; // Indicate failure.
        }
        catch (Exception ex)
        {
            // Other unexpected error during brush creation.
            Console.WriteLine($"Error creating brush for color {color}: {ex.Message}");
            return null!; // Indicate failure.
        }
    }


    public static void CleanupResources()
    {
        Console.WriteLine("UI Resource Cleanup: Disposing cached brushes...");
        int count = brushCache.Count;
        foreach (var pair in brushCache)
        {
            // Ensure value is not null before disposing
            pair.Value?.Dispose();
        }
        brushCache.Clear(); // Clear the dictionary
        Console.WriteLine($"UI Resource Cleanup finished. Disposed {count} brushes.");

        // Clear layout stack state
        containerStack.Clear();

        // Optional: Dispose UI elements if they implement IDisposable
        // This is less common in pure immediate mode unless elements hold heavy native resources.
        // Console.WriteLine("UI Resource Cleanup: Disposing stateful elements (if any)...");
        // foreach(var pair in uiElements)
        // {
        //    if (pair.Value is IDisposable disposableElement)
        //    {
        //        try { disposableElement.Dispose(); } catch (Exception ex) { Console.WriteLine($"Error disposing element {pair.Key}: {ex.Message}"); }
        //    }
        // }
        // uiElements.Clear(); // Clearing elements means state is lost between frames if cleanup is frequent
        // Console.WriteLine("UI Resource Cleanup: Element disposal finished.");
    }
}