// MODIFIED: UI.cs
// Summary: Added PushAxisAlignedClip/PopAxisAlignedClip within widget methods when inside a GridContainer.
using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1; // Alias for convenience

namespace DirectUI;

// Enums (ensure they exist)
public enum HSliderDirection { LeftToRight, RightToLeft }
public enum VSliderDirection { TopToBottom, BottomToTop }


public static class UI
{
    private static ID2D1HwndRenderTarget? currentRenderTarget;
    private static IDWriteFactory? currentDWriteFactory;
    private static InputState currentInputState;
    private static readonly Dictionary<string, object> uiElements = new();
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
            containerStack.Clear();
        }
        currentRenderTarget = null;
        currentDWriteFactory = null;
    }

    // --- Containers (Begin/End HBox, VBox, Grid as before) ---
    public static void BeginHBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Vector2 startPosition = ApplyLayout(position);
        var containerState = new HBoxContainerState(id, startPosition, gap);
        containerStack.Push(containerState);
    }
    public static void EndHBoxContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not HBoxContainerState state) { Console.WriteLine("Error: EndHBoxContainer mismatch."); return; }
        containerStack.Pop();
        if (IsInLayoutContainer()) { AdvanceLayoutCursor(new Vector2(state.AccumulatedWidth, state.MaxElementHeight)); }
    }
    public static void BeginVBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Vector2 startPosition = ApplyLayout(position);
        var containerState = new VBoxContainerState(id, startPosition, gap);
        containerStack.Push(containerState);
    }
    public static void EndVBoxContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not VBoxContainerState state) { Console.WriteLine("Error: EndVBoxContainer mismatch."); return; }
        containerStack.Pop();
        if (IsInLayoutContainer()) { AdvanceLayoutCursor(new Vector2(state.MaxElementWidth, state.AccumulatedHeight)); }
    }
    public static void BeginGridContainer(string id, Vector2 position, Vector2 availableSize, int numColumns, Vector2 gap)
    {
        Vector2 startPosition = ApplyLayout(position);
        var containerState = new GridContainerState(id, startPosition, availableSize, numColumns, gap);
        containerStack.Push(containerState);
    }
    public static void EndGridContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not GridContainerState state) { Console.WriteLine("Error: EndGridContainer mismatch."); return; }
        containerStack.Pop();
        if (IsInLayoutContainer())
        {
            if (state.RowHeights.Count > 0)
            {
                state.RowHeights[^1] = state.CurrentRowMaxHeight;
                state.AccumulatedHeight += state.CurrentRowMaxHeight;
                if (state.RowHeights.Count > 1) state.AccumulatedHeight += state.Gap.Y;
            }
            Vector2 containerSize = state.GetTotalOccupiedSize();
            AdvanceLayoutCursor(containerSize);
        }
    }

    // --- Widgets (Button, Sliders) ---
    public static bool Button(string id, ButtonDefinition definition)
    {
        if (!IsContextValid() || definition is null) return false;

        Button buttonInstance = GetOrCreateElement<Button>(id);
        Vector2 elementPosition = GetCurrentLayoutPositionInternal();
        buttonInstance.Position = elementPosition;

        ApplyButtonDefinition(buttonInstance, definition);

        // --- Prepare for Draw (Handle Clipping) ---
        bool pushedClip = false;
        if (IsInLayoutContainer() && containerStack.Peek() is GridContainerState grid)
        {
            // Calculate clip rect based on cell position and width. Height is tricky, use remaining available or large value.
            float clipHeight = grid.StartPosition.Y + grid.AvailableSize.Y - grid.CurrentDrawPosition.Y;
            Rect clipRect = new Rect(
                grid.CurrentDrawPosition.X,
                grid.CurrentDrawPosition.Y,
                Math.Max(0f, grid.CellWidth), // Ensure non-negative width
                Math.Max(0f, clipHeight)      // Ensure non-negative height
            );
            CurrentRenderTarget!.PushAxisAlignedClip(clipRect, D2D.AntialiasMode.Aliased);
            pushedClip = true;
        }

        // --- Update State and Draw ---
        buttonInstance.UpdateStyle();
        if (CurrentDWriteFactory is not null) buttonInstance.PerformAutoWidth(CurrentDWriteFactory);
        bool clicked = buttonInstance.Update(); // This now draws within the clip rect if pushed

        // --- Clean up Draw State ---
        if (pushedClip)
        {
            CurrentRenderTarget!.PopAxisAlignedClip();
        }

        // --- Advance Layout ---
        AdvanceLayout(buttonInstance.Size);

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

        // --- Prepare for Draw (Handle Clipping) ---
        bool pushedClip = false;
        if (IsInLayoutContainer() && containerStack.Peek() is GridContainerState grid)
        {
            float clipHeight = grid.StartPosition.Y + grid.AvailableSize.Y - grid.CurrentDrawPosition.Y;
            Rect clipRect = new Rect(
                grid.CurrentDrawPosition.X,
                grid.CurrentDrawPosition.Y,
                Math.Max(0f, grid.CellWidth),
                Math.Max(0f, clipHeight)
            );
            CurrentRenderTarget!.PushAxisAlignedClip(clipRect, D2D.AntialiasMode.Aliased);
            pushedClip = true;
        }

        // --- Update State and Draw ---
        float newValue = sliderInstance.UpdateAndDraw(CurrentInputState, GetCurrentDrawingContext(), currentValue);

        // --- Clean up Draw State ---
        if (pushedClip)
        {
            CurrentRenderTarget!.PopAxisAlignedClip();
        }

        // --- Advance Layout ---
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

        // --- Prepare for Draw (Handle Clipping) ---
        bool pushedClip = false;
        if (IsInLayoutContainer() && containerStack.Peek() is GridContainerState grid)
        {
            float clipHeight = grid.StartPosition.Y + grid.AvailableSize.Y - grid.CurrentDrawPosition.Y;
            Rect clipRect = new Rect(
                grid.CurrentDrawPosition.X,
                grid.CurrentDrawPosition.Y,
                Math.Max(0f, grid.CellWidth),
                Math.Max(0f, clipHeight)
            );
            CurrentRenderTarget!.PushAxisAlignedClip(clipRect, D2D.AntialiasMode.Aliased);
            pushedClip = true;
        }

        // --- Update State and Draw ---
        float newValue = sliderInstance.UpdateAndDraw(CurrentInputState, GetCurrentDrawingContext(), currentValue);

        // --- Clean up Draw State ---
        if (pushedClip)
        {
            CurrentRenderTarget!.PopAxisAlignedClip();
        }

        // --- Advance Layout ---
        AdvanceLayout(sliderInstance.Size);

        return newValue;
    }


    // --- Helper Methods (IsContextValid, GetCurrentDrawingContext, GetOrCreateElement, ApplyButton/SliderDefinition) ---
    // These remain unchanged from the previous version...
    private static bool IsContextValid() { /* ... */ return CurrentRenderTarget is not null && CurrentDWriteFactory is not null; }
    private static DrawingContext GetCurrentDrawingContext() { /* ... */ return new DrawingContext(CurrentRenderTarget!, CurrentDWriteFactory!); }
    private static T GetOrCreateElement<T>(string id) where T : new() { /* ... */ if (uiElements.TryGetValue(id, out object? element) && element is T existingElement) return existingElement; T newElement = new(); uiElements[id] = newElement; return newElement; }
    private static void ApplyButtonDefinition(Button instance, ButtonDefinition definition) { /* ... */ instance.Size = definition.Size; instance.Text = definition.Text; instance.Themes = definition.Theme ?? instance.Themes ?? new ButtonStylePack(); instance.Origin = definition.Origin ?? Vector2.Zero; instance.TextAlignment = definition.TextAlignment ?? new Alignment(HAlignment.Center, VAlignment.Center); instance.TextOffset = definition.TextOffset ?? Vector2.Zero; instance.AutoWidth = definition.AutoWidth; instance.TextMargin = definition.TextMargin ?? new Vector2(10, 5); instance.Behavior = definition.Behavior; instance.LeftClickActionMode = definition.LeftClickActionMode; instance.Disabled = definition.Disabled; instance.UserData = definition.UserData; }
    private static void ApplySliderDefinition(InternalSliderLogic instance, SliderDefinition definition) { /* ... */ instance.Size = definition.Size; instance.MinValue = definition.MinValue; instance.MaxValue = definition.MaxValue; instance.Step = definition.Step; instance.Theme = definition.Theme ?? instance.Theme ?? new SliderStyle(); instance.GrabberTheme = definition.GrabberTheme ?? instance.GrabberTheme ?? new ButtonStylePack(); instance.GrabberSize = definition.GrabberSize ?? instance.GrabberSize; instance.Origin = definition.Origin ?? Vector2.Zero; instance.Disabled = definition.Disabled; instance.UserData = definition.UserData; }


    // --- Layout Helpers (ApplyLayout, AdvanceLayout, IsInLayoutContainer, GetCurrentLayoutPositionInternal/Public, AdvanceLayoutCursor) ---
    // These remain unchanged from the previous version...
    private static Vector2 ApplyLayout(Vector2 defaultPosition) { return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : defaultPosition; }
    private static void AdvanceLayout(Vector2 elementSize) { if (IsInLayoutContainer()) { AdvanceLayoutCursor(elementSize); } }
    private static bool IsInLayoutContainer() => containerStack.Count > 0;
    private static Vector2 GetCurrentLayoutPositionInternal() { if (!IsInLayoutContainer()) return Vector2.Zero; object state = containerStack.Peek(); return state switch { HBoxContainerState hbox => hbox.CurrentPosition, VBoxContainerState vbox => vbox.CurrentPosition, GridContainerState grid => grid.CurrentDrawPosition, _ => Vector2.Zero }; }
    public static Vector2 GetCurrentLayoutPosition() { return GetCurrentLayoutPositionInternal(); }
    private static void AdvanceLayoutCursor(Vector2 elementSize) { object state = containerStack.Peek(); switch (state) { case HBoxContainerState hbox: if (elementSize.Y > hbox.MaxElementHeight) hbox.MaxElementHeight = elementSize.Y; float advX = (hbox.AccumulatedWidth > 0 ? hbox.Gap : 0) + elementSize.X; hbox.CurrentPosition = new Vector2(hbox.CurrentPosition.X + advX, hbox.CurrentPosition.Y); hbox.AccumulatedWidth = hbox.CurrentPosition.X - hbox.StartPosition.X; break; case VBoxContainerState vbox: if (elementSize.X > vbox.MaxElementWidth) vbox.MaxElementWidth = elementSize.X; float advY = (vbox.AccumulatedHeight > 0 ? vbox.Gap : 0) + elementSize.Y; vbox.CurrentPosition = new Vector2(vbox.CurrentPosition.X, vbox.CurrentPosition.Y + advY); vbox.AccumulatedHeight = vbox.CurrentPosition.Y - vbox.StartPosition.Y; break; case GridContainerState grid: grid.MoveToNextCell(elementSize); break; } }


    // --- Brush Cache & Cleanup (GetOrCreateBrush, CleanupResources) ---
    // These remain unchanged from the previous version...
    public static ID2D1SolidColorBrush GetOrCreateBrush(Color4 color) { if (CurrentRenderTarget is null) { Console.WriteLine("Error: GetOrCreateBrush called with no active render target."); return null!; } if (brushCache.TryGetValue(color, out ID2D1SolidColorBrush? brush) && brush is not null) { return brush; } else if (brush is null && brushCache.ContainsKey(color)) { brushCache.Remove(color); } try { brush = CurrentRenderTarget.CreateSolidColorBrush(color); if (brush is not null) { brushCache[color] = brush; return brush; } else { Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}"); return null!; } } catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code) { Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). External cleanup needed."); return null!; } catch (Exception ex) { Console.WriteLine($"Error creating brush for color {color}: {ex.Message}"); return null!; } }
    public static void CleanupResources() { Console.WriteLine("UI Resource Cleanup: Disposing cached brushes..."); int count = brushCache.Count; foreach (var pair in brushCache) { pair.Value?.Dispose(); } brushCache.Clear(); Console.WriteLine($"UI Resource Cleanup finished. Disposed {count} brushes."); containerStack.Clear(); }
}