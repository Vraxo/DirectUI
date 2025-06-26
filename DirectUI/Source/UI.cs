// MODIFIED: UI.cs
// Summary: Rewrote DrawBoxStyleHelper for both rounded and non-rounded rectangles. Rounded now draws border area first, then fill area on top. Non-rounded also draws border first, then fill.
using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public enum HSliderDirection { LeftToRight, RightToLeft }
public enum VSliderDirection { TopToBottom, BottomToTop }

public static class UI
{
    // --- State fields ---
    private static ID2D1HwndRenderTarget? currentRenderTarget;
    private static IDWriteFactory? currentDWriteFactory;
    private static InputState currentInputState;
    private static readonly Dictionary<string, object> uiElements = new();
    private static readonly Dictionary<Color4, ID2D1SolidColorBrush> brushCache = new();
    private static readonly Stack<object> containerStack = new();

    // Input State
    private static bool captureAttemptedThisFrame = false;
    private static string? inputCaptorId = null;
    private static string? potentialInputTargetId = null;
    private static string? activelyPressedElementId = null;
    internal static bool dragInProgressFromPreviousFrame = false;
    internal static bool nonSliderElementClaimedPress = false;

    // --- Public/Internal Properties ---
    public static ID2D1HwndRenderTarget? CurrentRenderTarget => currentRenderTarget;
    public static IDWriteFactory? CurrentDWriteFactory => currentDWriteFactory;
    public static InputState CurrentInputState => currentInputState;
    public static string? ActivelyPressedElementId => activelyPressedElementId;
    public static string? InputCaptorId => inputCaptorId;
    internal static string? PotentialInputTargetId => potentialInputTargetId;

    // --- Frame Management ---
    public static void BeginFrame(DrawingContext context, InputState input)
    {
        currentRenderTarget = context.RenderTarget;
        currentDWriteFactory = context.DWriteFactory;
        currentInputState = input;
        containerStack.Clear();

        dragInProgressFromPreviousFrame = input.IsLeftMouseDown && activelyPressedElementId is not null;

        captureAttemptedThisFrame = false;
        inputCaptorId = null;
        potentialInputTargetId = null;
        nonSliderElementClaimedPress = false;

        if (!input.IsLeftMouseDown)
        {
            activelyPressedElementId = null;
        }
    }

    public static void EndFrame()
    {
        if (containerStack.Count > 0)
        {
            Console.WriteLine($"Warning: Mismatch in Begin/End container calls. {containerStack.Count} containers left open at EndFrame.");
            containerStack.Clear();
        }
        currentRenderTarget = null;
        currentDWriteFactory = null;
    }

    // --- Input Capture & Targeting ---

    public static bool IsElementActive()
    {
        return activelyPressedElementId is not null;
    }

    internal static void SetPotentialInputTarget(string id)
    {
        potentialInputTargetId = id;
    }

    internal static void SetPotentialCaptorForFrame(string id)
    {
        captureAttemptedThisFrame = true;
        inputCaptorId = id;
        activelyPressedElementId = id;
    }

    internal static void SetButtonPotentialCaptorForFrame(string id)
    {
        captureAttemptedThisFrame = true;
        inputCaptorId = id;
        activelyPressedElementId = id;
        nonSliderElementClaimedPress = true;
    }

    internal static void ClearActivePress(string id)
    {
        if (activelyPressedElementId == id)
        {
            activelyPressedElementId = null;
        }
    }

    // --- Containers ---
    public static void BeginHBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Vector2 startPosition = ApplyLayout(position);
        var containerState = new HBoxContainerState(id, startPosition, gap);
        containerStack.Push(containerState);
    }
    public static void EndHBoxContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not HBoxContainerState state)
        { Console.WriteLine("Error: EndHBoxContainer called without a matching BeginHBoxContainer."); return; }
        containerStack.Pop();
        if (IsInLayoutContainer())
        { AdvanceLayoutCursor(new Vector2(state.AccumulatedWidth, state.MaxElementHeight)); }
    }
    public static void BeginVBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Vector2 startPosition = ApplyLayout(position);
        var containerState = new VBoxContainerState(id, startPosition, gap);
        containerStack.Push(containerState);
    }
    public static void EndVBoxContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not VBoxContainerState state)
        { Console.WriteLine("Error: EndVBoxContainer called without a matching BeginVBoxContainer."); return; }
        containerStack.Pop();
        if (IsInLayoutContainer())
        { AdvanceLayoutCursor(new Vector2(state.MaxElementWidth, state.AccumulatedHeight)); }
    }
    public static void BeginGridContainer(string id, Vector2 position, Vector2 availableSize, int numColumns, Vector2 gap)
    {
        Vector2 startPosition = ApplyLayout(position);
        var containerState = new GridContainerState(id, startPosition, availableSize, numColumns, gap);
        containerStack.Push(containerState);
    }
    public static void EndGridContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not GridContainerState state)
        { Console.WriteLine("Error: EndGridContainer called without a matching BeginGridContainer."); return; }
        containerStack.Pop();
        if (IsInLayoutContainer())
        {
            if (state.RowHeights.Count > 0 && state.CurrentCellIndex > 0)
            {
                int lastPopulatedRowIndex = (state.CurrentCellIndex - 1) / state.NumColumns;
                if (lastPopulatedRowIndex < state.RowHeights.Count)
                { state.RowHeights[lastPopulatedRowIndex] = state.CurrentRowMaxHeight; }
                else { Console.WriteLine($"Warning: Grid '{state.Id}' - Row index mismatch during EndGridContainer."); }
                state.AccumulatedHeight = 0f; bool firstRowAdded = false;
                for (int i = 0; i < state.RowHeights.Count; i++) { if (state.RowHeights[i] > 0) { if (firstRowAdded) { state.AccumulatedHeight += state.Gap.Y; } state.AccumulatedHeight += state.RowHeights[i]; firstRowAdded = true; } }
            }
            Vector2 containerSize = state.GetTotalOccupiedSize();
            AdvanceLayoutCursor(containerSize);
        }
    }

    // --- Widgets ---
    public static bool Button(string id, ButtonDefinition definition)
    {
        if (!IsContextValid() || definition is null) return false;
        Button buttonInstance = GetOrCreateElement<Button>(id);
        Vector2 elementPosition = GetCurrentLayoutPositionInternal();
        buttonInstance.Position = elementPosition;
        ApplyButtonDefinition(buttonInstance, definition);
        bool pushedClip = false; Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer() && containerStack.Peek() is GridContainerState grid)
        {
            float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
            float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
            cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
            if (CurrentRenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
            { CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
        }
        bool clicked = buttonInstance.Update(id);
        if (pushedClip && CurrentRenderTarget is not null)
        { CurrentRenderTarget.PopAxisAlignedClip(); }
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
        bool pushedClip = false; Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer() && containerStack.Peek() is GridContainerState grid)
        {
            float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
            float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
            cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
            if (CurrentRenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
            { CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
        }
        float newValue = sliderInstance.UpdateAndDraw(id, CurrentInputState, GetCurrentDrawingContext(), currentValue);
        if (pushedClip && CurrentRenderTarget is not null)
        { CurrentRenderTarget.PopAxisAlignedClip(); }
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
        bool pushedClip = false; Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer() && containerStack.Peek() is GridContainerState grid)
        {
            float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
            float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
            cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
            if (CurrentRenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
            { CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
        }
        float newValue = sliderInstance.UpdateAndDraw(id, CurrentInputState, GetCurrentDrawingContext(), currentValue);
        if (pushedClip && CurrentRenderTarget is not null)
        { CurrentRenderTarget.PopAxisAlignedClip(); }
        AdvanceLayout(sliderInstance.Size);
        return newValue;
    }

    // --- Helper Methods ---
    private static bool IsContextValid()
    {
        if (CurrentRenderTarget is null || CurrentDWriteFactory is null) { Console.WriteLine($"Error: UI method called outside BeginFrame/EndFrame or context is invalid."); return false; }
        return true;
    }
    private static DrawingContext GetCurrentDrawingContext()
    {
        if (!IsContextValid()) { throw new InvalidOperationException("Attempted to get DrawingContext when UI context is invalid."); }
        return new DrawingContext(CurrentRenderTarget!, CurrentDWriteFactory!);
    }
    private static T GetOrCreateElement<T>(string id) where T : new()
    {
        if (uiElements.TryGetValue(id, out object? element) && element is T existingElement) { return existingElement; }
        else { T newElement = new(); uiElements[id] = newElement; return newElement; }
    }
    private static void ApplyButtonDefinition(Button instance, ButtonDefinition definition)
    {
        instance.Size = definition.Size; instance.Text = definition.Text; instance.Themes = definition.Theme ?? instance.Themes ?? new ButtonStylePack();
        instance.Origin = definition.Origin ?? Vector2.Zero; instance.TextAlignment = definition.TextAlignment ?? new Alignment(HAlignment.Center, VAlignment.Center);
        instance.TextOffset = definition.TextOffset ?? Vector2.Zero; instance.AutoWidth = definition.AutoWidth; instance.TextMargin = definition.TextMargin ?? new Vector2(10, 5);
        instance.Behavior = definition.Behavior; instance.LeftClickActionMode = definition.LeftClickActionMode; instance.Disabled = definition.Disabled; instance.UserData = definition.UserData;
    }
    private static void ApplySliderDefinition(InternalSliderLogic instance, SliderDefinition definition)
    {
        instance.Size = definition.Size; instance.MinValue = definition.MinValue; instance.MaxValue = definition.MaxValue; instance.Step = definition.Step;
        instance.Theme = definition.Theme ?? instance.Theme ?? new SliderStyle(); instance.GrabberTheme = definition.GrabberTheme ?? instance.GrabberTheme ?? new ButtonStylePack();
        instance.GrabberSize = definition.GrabberSize ?? instance.GrabberSize; instance.Origin = definition.Origin ?? Vector2.Zero; instance.Disabled = definition.Disabled; instance.UserData = definition.UserData;
    }

    // --- Layout Helpers ---
    private static Vector2 ApplyLayout(Vector2 defaultPosition) { return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : defaultPosition; }
    private static void AdvanceLayout(Vector2 elementSize) { if (IsInLayoutContainer()) { AdvanceLayoutCursor(new Vector2(Math.Max(0, elementSize.X), Math.Max(0, elementSize.Y))); } }
    private static bool IsInLayoutContainer() => containerStack.Count > 0;
    private static Vector2 GetCurrentLayoutPositionInternal()
    {
        if (containerStack.Count == 0) return Vector2.Zero;
        object currentContainerState = containerStack.Peek();
        return currentContainerState switch { HBoxContainerState hbox => hbox.CurrentPosition, VBoxContainerState vbox => vbox.CurrentPosition, GridContainerState grid => grid.CurrentDrawPosition, _ => Vector2.Zero, };
    }
    public static Vector2 GetCurrentLayoutPosition() { return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : Vector2.Zero; }
    private static void AdvanceLayoutCursor(Vector2 elementSize)
    {
        if (containerStack.Count == 0) return;
        object currentContainerState = containerStack.Peek();
        switch (currentContainerState)
        {
            case HBoxContainerState hbox: if (elementSize.Y > hbox.MaxElementHeight) hbox.MaxElementHeight = elementSize.Y; float advanceX = (hbox.AccumulatedWidth > 0 ? hbox.Gap : 0) + elementSize.X; hbox.CurrentPosition = new Vector2(hbox.CurrentPosition.X + advanceX, hbox.CurrentPosition.Y); hbox.AccumulatedWidth = hbox.CurrentPosition.X - hbox.StartPosition.X; break;
            case VBoxContainerState vbox: if (elementSize.X > vbox.MaxElementWidth) vbox.MaxElementWidth = elementSize.X; float advanceY = (vbox.AccumulatedHeight > 0 ? vbox.Gap : 0) + elementSize.Y; vbox.CurrentPosition = new Vector2(vbox.CurrentPosition.X, vbox.CurrentPosition.Y + advanceY); vbox.AccumulatedHeight = vbox.CurrentPosition.Y - vbox.StartPosition.Y; break;
            case GridContainerState grid: grid.MoveToNextCell(elementSize); break;
            default: Console.WriteLine("Error: AdvanceLayoutCursor called with unexpected container type."); break;
        }
    }

    // --- Brush Cache ---
    public static ID2D1SolidColorBrush GetOrCreateBrush(Color4 color)
    {
        if (CurrentRenderTarget is null) { Console.WriteLine("Error: GetOrCreateBrush called with no active render target."); return null!; }
        if (brushCache.TryGetValue(color, out ID2D1SolidColorBrush? brush) && brush is not null) { return brush; }
        else if (brush is null && brushCache.ContainsKey(color)) { brushCache.Remove(color); }
        try
        {
            brush = CurrentRenderTarget.CreateSolidColorBrush(color);
            if (brush is not null) { brushCache[color] = brush; return brush; }
            else { Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}"); return null!; }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code) { Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). External cleanup needed."); return null!; }
        catch (Exception ex) { Console.WriteLine($"Error creating brush for color {color}: {ex.Message}"); return null!; }
    }

    // --- Resource Cleanup ---
    public static void CleanupResources()
    {
        Console.WriteLine("UI Resource Cleanup: Disposing cached brushes..."); int count = brushCache.Count;
        foreach (var pair in brushCache) { pair.Value?.Dispose(); }
        brushCache.Clear(); Console.WriteLine($"UI Resource Cleanup finished. Disposed {count} brushes."); containerStack.Clear();
    }

    // --- SHARED DRAWING HELPERS ---
    internal static void DrawBoxStyleHelper(ID2D1RenderTarget renderTarget, Vector2 pos, Vector2 size, BoxStyle style)
    {
        if (renderTarget is null || style is null || size.X <= 0 || size.Y <= 0) return;

        ID2D1SolidColorBrush fillBrush = GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush borderBrush = GetOrCreateBrush(style.BorderColor);

        float borderTop = Math.Max(0f, style.BorderLengthTop);
        float borderRight = Math.Max(0f, style.BorderLengthRight);
        float borderBottom = Math.Max(0f, style.BorderLengthBottom);
        float borderLeft = Math.Max(0f, style.BorderLengthLeft);

        bool hasVisibleFill = style.FillColor.A > 0 && fillBrush is not null;
        bool hasVisibleBorder = style.BorderColor.A > 0 && borderBrush is not null && (borderTop > 0 || borderRight > 0 || borderBottom > 0 || borderLeft > 0);

        if (!hasVisibleFill && !hasVisibleBorder) return;

        // --- Rounded Rectangle Rendering (Layered Approach) ---
        if (style.Roundness > 0.0f)
        {
            Rect outerBounds = new Rect(pos.X, pos.Y, size.X, size.Y);
            float maxRadius = Math.Min(outerBounds.Width * 0.5f, outerBounds.Height * 0.5f);
            float radius = Math.Max(0f, maxRadius * Math.Clamp(style.Roundness, 0.0f, 1.0f));

            if (float.IsFinite(radius) && radius >= 0)
            {
                // 1. Draw Border Area (Outer Rounded Rectangle)
                if (hasVisibleBorder)
                {
                    System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                    RoundedRectangle outerRoundedRect = new(outerRectF, radius, radius);
                    renderTarget.FillRoundedRectangle(outerRoundedRect, borderBrush);
                }

                // 2. Draw Fill Area (Inner Rounded Rectangle on top)
                if (hasVisibleFill)
                {
                    float fillX = pos.X + borderLeft;
                    float fillY = pos.Y + borderTop;
                    float fillWidth = Math.Max(0f, size.X - borderLeft - borderRight);
                    float fillHeight = Math.Max(0f, size.Y - borderTop - borderBottom);

                    if (fillWidth > 0 && fillHeight > 0)
                    {
                        // Adjust inner radius - clamp at zero. Use average border thickness for approximation.
                        float avgBorderX = (borderLeft + borderRight) * 0.5f;
                        float avgBorderY = (borderTop + borderBottom) * 0.5f;
                        float innerRadiusX = Math.Max(0f, radius - avgBorderX);
                        float innerRadiusY = Math.Max(0f, radius - avgBorderY);

                        System.Drawing.RectangleF fillRectF = new(fillX, fillY, fillWidth, fillHeight);
                        RoundedRectangle fillRoundedRect = new(fillRectF, innerRadiusX, innerRadiusY);
                        renderTarget.FillRoundedRectangle(fillRoundedRect, fillBrush);
                    }
                    // If fill consumes the whole area (e.g., no border), draw it directly using outer bounds/radius
                    else if (!hasVisibleBorder && fillBrush is not null)
                    {
                        System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                        RoundedRectangle outerRoundedRect = new(outerRectF, radius, radius);
                        renderTarget.FillRoundedRectangle(outerRoundedRect, fillBrush);
                    }
                }
                return; // Handled rounded case
            }
            // Fall through to sharp rendering if radius calculation failed
        }

        // --- Non-Rounded Rectangle Rendering (Layered Approach) ---

        // 1. Draw Border Area (Outer Rectangles)
        if (hasVisibleBorder && borderBrush is not null)
        {
            // Fill the entire background with border color first
            renderTarget.FillRectangle(new Rect(pos.X, pos.Y, size.X, size.Y), borderBrush);
        }

        // 2. Draw Fill Area (Inner Rectangle on top)
        if (hasVisibleFill)
        {
            float fillX = pos.X + borderLeft;
            float fillY = pos.Y + borderTop;
            float fillWidth = Math.Max(0f, size.X - borderLeft - borderRight);
            float fillHeight = Math.Max(0f, size.Y - borderTop - borderBottom);

            if (fillWidth > 0 && fillHeight > 0)
            {
                renderTarget.FillRectangle(new Rect(fillX, fillY, fillWidth, fillHeight), fillBrush);
            }
            // If fill consumes the whole area (e.g., no border), draw it directly
            else if (!hasVisibleBorder && fillBrush is not null)
            {
                renderTarget.FillRectangle(new Rect(pos.X, pos.Y, size.X, size.Y), fillBrush);
            }
        }
    }
}