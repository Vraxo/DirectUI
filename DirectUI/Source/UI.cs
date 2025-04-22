// MODIFIED: UI.cs
// Summary: Added HSlider and VSlider immediate-mode methods.
using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

// Need definitions if not already present from Cherris example
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
            containerStack.Clear();
        }
        currentRenderTarget = null;
        currentDWriteFactory = null;
    }

    // --- Containers ---
    public static void BeginHBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Vector2 startPosition = IsInLayoutContainer() ? GetCurrentLayoutPosition() : position;
        var containerState = new HBoxContainerState(id, startPosition, gap);
        containerStack.Push(containerState);
    }
    public static void EndHBoxContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not HBoxContainerState) { /* Error */ return; }
        HBoxContainerState finishedContainer = (HBoxContainerState)containerStack.Pop();
        if (IsInLayoutContainer())
        {
            Vector2 containerSize = new Vector2(finishedContainer.AccumulatedWidth, finishedContainer.MaxElementHeight);
            AdvanceLayoutCursor(containerSize);
        }
    }
    public static void BeginVBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Vector2 startPosition = IsInLayoutContainer() ? GetCurrentLayoutPosition() : position;
        var containerState = new VBoxContainerState(id, startPosition, gap);
        containerStack.Push(containerState);
    }
    public static void EndVBoxContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not VBoxContainerState) { /* Error */ return; }
        VBoxContainerState finishedContainer = (VBoxContainerState)containerStack.Pop();
        if (IsInLayoutContainer())
        {
            Vector2 containerSize = new Vector2(finishedContainer.MaxElementWidth, finishedContainer.AccumulatedHeight);
            AdvanceLayoutCursor(containerSize);
        }
    }

    // --- Button ---
    public static bool Button(string id, ButtonDefinition definition)
    {
        if (!IsContextValid() || definition is null) return false;

        Button buttonInstance = GetOrCreateElement<Button>(id);

        Vector2 elementPosition = ApplyLayout(definition.Position);
        buttonInstance.Position = elementPosition;

        ApplyButtonDefinition(buttonInstance, definition);

        buttonInstance.UpdateStyle(); // Needs state from previous frame potentially
        if (CurrentDWriteFactory is not null)
        {
            buttonInstance.PerformAutoWidth(CurrentDWriteFactory);
        }

        bool clicked = buttonInstance.Update(); // Update draws and returns click state

        AdvanceLayout(buttonInstance.Size);

        return clicked;
    }

    // --- HSlider ---
    public static float HSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue; // Return input value on error

        InternalHSliderLogic sliderInstance = GetOrCreateElement<InternalHSliderLogic>(id);

        Vector2 elementPosition = ApplyLayout(definition.Position);
        sliderInstance.Position = elementPosition;

        // Apply definition properties to the internal instance
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.HorizontalDirection; // Set specific direction

        // UpdateAndDraw performs logic, drawing, and returns the new value
        float newValue = sliderInstance.UpdateAndDraw(CurrentInputState, GetCurrentDrawingContext(), currentValue);

        AdvanceLayout(sliderInstance.Size); // Use the slider's size for layout

        return newValue;
    }

    // --- VSlider ---
    public static float VSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;

        InternalVSliderLogic sliderInstance = GetOrCreateElement<InternalVSliderLogic>(id);

        Vector2 elementPosition = ApplyLayout(definition.Position);
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
        // Note: Position is set by ApplyLayout
        // Note: Direction is set specifically in HSlider/VSlider methods
    }


    // --- Layout Helpers ---
    private static Vector2 ApplyLayout(Vector2 defaultPosition)
    {
        return IsInLayoutContainer() ? GetCurrentLayoutPosition() : defaultPosition;
    }
    private static void AdvanceLayout(Vector2 elementSize)
    {
        if (IsInLayoutContainer())
        {
            AdvanceLayoutCursor(elementSize);
        }
    }
    private static bool IsInLayoutContainer() => containerStack.Count > 0;
    private static Vector2 GetCurrentLayoutPosition()
    {
        if (!IsInLayoutContainer()) return Vector2.Zero;
        object currentContainerState = containerStack.Peek();
        return currentContainerState switch
        {
            HBoxContainerState hbox => hbox.CurrentPosition,
            VBoxContainerState vbox => vbox.CurrentPosition,
            _ => Vector2.Zero, // Error case
        };
    }
    private static void AdvanceLayoutCursor(Vector2 elementSize)
    {
        if (!IsInLayoutContainer()) return;
        object currentContainerState = containerStack.Peek();

        switch (currentContainerState)
        {
            case HBoxContainerState hbox:
                if (elementSize.Y > hbox.MaxElementHeight) hbox.MaxElementHeight = elementSize.Y;
                float advanceX = (hbox.AccumulatedWidth > 0 ? hbox.Gap : 0) + elementSize.X;
                hbox.CurrentPosition = new Vector2(hbox.CurrentPosition.X + advanceX, hbox.CurrentPosition.Y);
                hbox.AccumulatedWidth += advanceX; // This accumulation seems off, recalculate from start
                hbox.AccumulatedWidth = hbox.CurrentPosition.X - hbox.StartPosition.X; // Correct way
                break;

            case VBoxContainerState vbox:
                if (elementSize.X > vbox.MaxElementWidth) vbox.MaxElementWidth = elementSize.X;
                float advanceY = (vbox.AccumulatedHeight > 0 ? vbox.Gap : 0) + elementSize.Y;
                vbox.CurrentPosition = new Vector2(vbox.CurrentPosition.X, vbox.CurrentPosition.Y + advanceY);
                vbox.AccumulatedHeight = vbox.CurrentPosition.Y - vbox.StartPosition.Y; // Correct way
                break;
        }
    }
    // --- End Layout Helpers ---


    public static ID2D1SolidColorBrush GetOrCreateBrush(Color4 color)
    {
        if (CurrentRenderTarget is null) return null!; // Context check needed before calling

        if (brushCache.TryGetValue(color, out ID2D1SolidColorBrush? brush) && brush is not null)
        {
            return brush;
        }
        else if (brush is null && brushCache.ContainsKey(color))
        {
            brushCache.Remove(color);
        }

        try
        {
            brush = CurrentRenderTarget.CreateSolidColorBrush(color);
            if (brush is not null) { brushCache[color] = brush; return brush; }
            else { Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}"); return null!; }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        { Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). External cleanup needed."); return null!; }
        catch (Exception ex)
        { Console.WriteLine($"Error creating brush for color {color}: {ex.Message}"); return null!; }
    }


    public static void CleanupResources()
    {
        Console.WriteLine("UI Resource Cleanup: Disposing cached brushes...");
        int count = brushCache.Count;
        foreach (var pair in brushCache) { pair.Value?.Dispose(); }
        brushCache.Clear();
        Console.WriteLine($"UI Resource Cleanup finished. Disposed {count} brushes.");
        containerStack.Clear();
        // We might want to clear uiElements if elements become disposable, but
        // for now they are just state holders reused frame-to-frame.
    }
}