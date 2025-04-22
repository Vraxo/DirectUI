// UI.cs
using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1; // Alias for convenience

namespace DirectUI;

// Enums (ensure they exist or are defined here)
public enum HSliderDirection { LeftToRight, RightToLeft }
public enum VSliderDirection { TopToBottom, BottomToTop }

public static class UI
{
    // --- State fields ---
    private static ID2D1HwndRenderTarget? currentRenderTarget;
    private static IDWriteFactory? currentDWriteFactory;
    private static InputState currentInputState;
    private static readonly Dictionary<string, object> uiElements = new(); // Stores Buttons, Sliders etc.
    private static readonly Dictionary<Color4, ID2D1SolidColorBrush> brushCache = new();
    private static readonly Stack<object> containerStack = new(); // Stores HBox, VBox, Grid states

    // --- Public Properties ---
    public static ID2D1HwndRenderTarget? CurrentRenderTarget => currentRenderTarget;
    public static IDWriteFactory? CurrentDWriteFactory => currentDWriteFactory;
    public static InputState CurrentInputState => currentInputState;

    // --- Frame Management ---
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
            Console.WriteLine($"Warning: Mismatch in Begin/End container calls. {containerStack.Count} containers left open at EndFrame.");
            containerStack.Clear();
        }
        currentRenderTarget = null;
        currentDWriteFactory = null;
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
        {
            Console.WriteLine("Error: EndHBoxContainer called without a matching BeginHBoxContainer."); return;
        }
        containerStack.Pop();
        if (IsInLayoutContainer())
        {
            Vector2 containerSize = new Vector2(state.AccumulatedWidth, state.MaxElementHeight);
            AdvanceLayoutCursor(containerSize);
        }
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
        {
            Console.WriteLine("Error: EndVBoxContainer called without a matching BeginVBoxContainer."); return;
        }
        containerStack.Pop();
        if (IsInLayoutContainer())
        {
            Vector2 containerSize = new Vector2(state.MaxElementWidth, state.AccumulatedHeight);
            AdvanceLayoutCursor(containerSize);
        }
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
        {
            Console.WriteLine("Error: EndGridContainer called without a matching BeginGridContainer."); return;
        }
        containerStack.Pop();
        if (IsInLayoutContainer())
        {
            if (state.RowHeights.Count > 0)
            {
                state.RowHeights[^1] = state.CurrentRowMaxHeight;
                state.AccumulatedHeight += state.CurrentRowMaxHeight;
                if (state.RowHeights.Count > 1)
                {
                    state.AccumulatedHeight += state.Gap.Y;
                }
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

        bool pushedClip = false;
        Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer() && containerStack.Peek() is GridContainerState grid)
        {
            float clipHeight = grid.StartPosition.Y + grid.AvailableSize.Y - grid.CurrentDrawPosition.Y;
            cellClipRect = new Rect(
                grid.CurrentDrawPosition.X,
                grid.CurrentDrawPosition.Y,
                Math.Max(0f, grid.CellWidth),
                Math.Max(0f, clipHeight)
            );
            if (CurrentRenderTarget is not null)
            {
                CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased);
                pushedClip = true;
            }
        }

        bool clicked = buttonInstance.Update();

        if (pushedClip && CurrentRenderTarget is not null)
        {
            CurrentRenderTarget.PopAxisAlignedClip();
        }

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

        bool pushedClip = false;
        Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer() && containerStack.Peek() is GridContainerState grid)
        {
            float clipHeight = grid.StartPosition.Y + grid.AvailableSize.Y - grid.CurrentDrawPosition.Y;
            cellClipRect = new Rect(
                grid.CurrentDrawPosition.X,
                grid.CurrentDrawPosition.Y,
                Math.Max(0f, grid.CellWidth),
                Math.Max(0f, clipHeight)
            );
            if (CurrentRenderTarget is not null)
            {
                CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased);
                pushedClip = true;
            }
        }

        float newValue = sliderInstance.UpdateAndDraw(CurrentInputState, GetCurrentDrawingContext(), currentValue);

        if (pushedClip && CurrentRenderTarget is not null)
        {
            CurrentRenderTarget.PopAxisAlignedClip();
        }

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

        bool pushedClip = false;
        Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer() && containerStack.Peek() is GridContainerState grid)
        {
            float clipHeight = grid.StartPosition.Y + grid.AvailableSize.Y - grid.CurrentDrawPosition.Y;
            cellClipRect = new Rect(
                grid.CurrentDrawPosition.X,
                grid.CurrentDrawPosition.Y,
                Math.Max(0f, grid.CellWidth),
                Math.Max(0f, clipHeight)
            );
            if (CurrentRenderTarget is not null)
            {
                CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased);
                pushedClip = true;
            }
        }

        float newValue = sliderInstance.UpdateAndDraw(CurrentInputState, GetCurrentDrawingContext(), currentValue);

        if (pushedClip && CurrentRenderTarget is not null)
        {
            CurrentRenderTarget.PopAxisAlignedClip();
        }

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
        if (!IsContextValid())
        {
            throw new InvalidOperationException("Attempted to get DrawingContext when UI context is invalid.");
        }
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
            T newElement = new();
            uiElements[id] = newElement;
            return newElement;
        }
    }
    private static void ApplyButtonDefinition(Button instance, ButtonDefinition definition)
    {
        instance.Size = definition.Size;
        instance.Text = definition.Text;
        instance.Themes = definition.Theme ?? instance.Themes ?? new ButtonStylePack();
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
        instance.Size = definition.Size;
        instance.MinValue = definition.MinValue;
        instance.MaxValue = definition.MaxValue;
        instance.Step = definition.Step;
        instance.Theme = definition.Theme ?? instance.Theme ?? new SliderStyle();
        instance.GrabberTheme = definition.GrabberTheme ?? instance.GrabberTheme ?? new ButtonStylePack();
        instance.GrabberSize = definition.GrabberSize ?? instance.GrabberSize;
        instance.Origin = definition.Origin ?? Vector2.Zero;
        instance.Disabled = definition.Disabled;
        instance.UserData = definition.UserData;
    }


    // --- Layout Helpers ---
    private static Vector2 ApplyLayout(Vector2 defaultPosition)
    {
        return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : defaultPosition;
    }
    private static void AdvanceLayout(Vector2 elementSize)
    {
        if (IsInLayoutContainer())
        {
            Vector2 nonNegativeSize = new Vector2(Math.Max(0, elementSize.X), Math.Max(0, elementSize.Y));
            AdvanceLayoutCursor(nonNegativeSize);
        }
    }
    private static bool IsInLayoutContainer() => containerStack.Count > 0;

    private static Vector2 GetCurrentLayoutPositionInternal()
    {
        object currentContainerState = containerStack.Peek();
        return currentContainerState switch
        {
            HBoxContainerState hbox => hbox.CurrentPosition,
            VBoxContainerState vbox => vbox.CurrentPosition,
            GridContainerState grid => grid.CurrentDrawPosition,
            _ => Vector2.Zero,
        };
    }

    public static Vector2 GetCurrentLayoutPosition()
    {
        return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : Vector2.Zero;
    }


    private static void AdvanceLayoutCursor(Vector2 elementSize)
    {
        object currentContainerState = containerStack.Peek();

        switch (currentContainerState)
        {
            case HBoxContainerState hbox:
                if (elementSize.Y > hbox.MaxElementHeight) hbox.MaxElementHeight = elementSize.Y;
                float advanceX = (hbox.AccumulatedWidth > 0 ? hbox.Gap : 0) + elementSize.X;
                hbox.CurrentPosition = new Vector2(hbox.CurrentPosition.X + advanceX, hbox.CurrentPosition.Y);
                hbox.AccumulatedWidth = hbox.CurrentPosition.X - hbox.StartPosition.X;
                break;

            case VBoxContainerState vbox:
                if (elementSize.X > vbox.MaxElementWidth) vbox.MaxElementWidth = elementSize.X;
                float advanceY = (vbox.AccumulatedHeight > 0 ? vbox.Gap : 0) + elementSize.Y;
                vbox.CurrentPosition = new Vector2(vbox.CurrentPosition.X, vbox.CurrentPosition.Y + advanceY);
                vbox.AccumulatedHeight = vbox.CurrentPosition.Y - vbox.StartPosition.Y;
                break;

            case GridContainerState grid:
                grid.MoveToNextCell(elementSize);
                break;
            default:
                Console.WriteLine("Error: AdvanceLayoutCursor called with unexpected container type.");
                break;
        }
    }
    // --- End Layout Helpers ---


    // --- Brush Cache ---
    public static ID2D1SolidColorBrush GetOrCreateBrush(Color4 color)
    {
        if (CurrentRenderTarget is null)
        {
            Console.WriteLine("Error: GetOrCreateBrush called with no active render target.");
            return null!;
        }

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
            if (brush is not null)
            {
                brushCache[color] = brush;
                return brush;
            }
            else
            {
                Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}");
                return null!;
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). External cleanup needed.");
            return null!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating brush for color {color}: {ex.Message}");
            return null!;
        }
    }


    // --- Resource Cleanup ---
    public static void CleanupResources()
    {
        Console.WriteLine("UI Resource Cleanup: Disposing cached brushes...");
        int count = brushCache.Count;
        foreach (var pair in brushCache)
        {
            pair.Value?.Dispose();
        }
        brushCache.Clear();
        Console.WriteLine($"UI Resource Cleanup finished. Disposed {count} brushes.");
        containerStack.Clear();
    }


    // --- SHARED DRAWING HELPERS --- FINAL VERSION ---

    /// <summary>
    /// Draws a BoxStyle. Supports independent border lengths for sharp corners.
    /// For rounded corners, it approximates using the max average border length
    /// and uses the standard Fill then Draw technique.
    /// </summary>
    internal static void DrawBoxStyleHelper(ID2D1RenderTarget renderTarget, Vector2 pos, Vector2 size, BoxStyle style)
    {
        if (renderTarget is null || style is null || size.X <= 0 || size.Y <= 0)
        {
            return; // Nothing to draw
        }

        // Get brushes
        ID2D1SolidColorBrush? fillBrush = GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush? borderBrush = GetOrCreateBrush(style.BorderColor);

        // Ensure border lengths are non-negative
        float borderTop = Math.Max(0f, style.BorderLengthTop);
        float borderRight = Math.Max(0f, style.BorderLengthRight);
        float borderBottom = Math.Max(0f, style.BorderLengthBottom);
        float borderLeft = Math.Max(0f, style.BorderLengthLeft);

        // Check visibility
        bool hasVisibleFill = style.FillColor.A > 0 && fillBrush is not null;
        bool hasVisibleBorder = style.BorderColor.A > 0 && borderBrush is not null &&
                                (borderTop > 0 || borderRight > 0 || borderBottom > 0 || borderLeft > 0);

        if (!hasVisibleFill && !hasVisibleBorder) return;

        // Store and set AntialiasMode for predictable shape rendering
        var originalAntialiasMode = renderTarget.AntialiasMode;
        renderTarget.AntialiasMode = AntialiasMode.PerPrimitive; // Default/standard for shapes

        try // Use finally block to ensure AA mode is restored
        {
            // --- Rounded Corners (Roundness > 0) ---
            if (style.Roundness > 0.0f)
            {
                // --- Standard Approximation: Fill then Draw ---
                float avgHorizontalBorder = (borderLeft + borderRight) / 2.0f;
                float avgVerticalBorder = (borderTop + borderBottom) / 2.0f; // Corrected variable name
                float approxBorderThickness = Math.Max(avgHorizontalBorder, avgVerticalBorder);

                Rect outerBounds = new Rect(pos.X, pos.Y, size.X, size.Y);
                // Prevent radius from being larger than half the size
                float maxOuterRadiusX = outerBounds.Width / 2.0f;
                float maxOuterRadiusY = outerBounds.Height / 2.0f;
                float outerRadiusX = Math.Min(maxOuterRadiusX, maxOuterRadiusX * Math.Clamp(style.Roundness, 0.0f, 1.0f));
                float outerRadiusY = Math.Min(maxOuterRadiusY, maxOuterRadiusY * Math.Clamp(style.Roundness, 0.0f, 1.0f));
                // Ensure non-negative radius
                outerRadiusX = Math.Max(0f, outerRadiusX);
                outerRadiusY = Math.Max(0f, outerRadiusY);


                if (float.IsFinite(outerRadiusX) && float.IsFinite(outerRadiusY))
                {
                    System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                    RoundedRectangle roundedRect = new(outerRectF, outerRadiusX, outerRadiusY); // Use potentially different X/Y radius

                    // 1. Draw Fill
                    if (hasVisibleFill && fillBrush is not null)
                    {
                        renderTarget.FillRoundedRectangle(roundedRect, fillBrush);
                    }

                    // 2. Draw Border (using approximated thickness)
                    if (hasVisibleBorder && borderBrush is not null && approxBorderThickness > 0)
                    {
                        // Use the SAME roundedRect definition
                        renderTarget.DrawRoundedRectangle(roundedRect, borderBrush, approxBorderThickness);
                    }
                    return; // Finished rounded drawing
                }
                // Fallback to sharp corners if radius calculation resulted in NaN/Infinity
            }

            // --- Sharp Corners (Roundness <= 0 or Fallback) ---
            // This path is taken if Roundness <= 0 or if rounded calculation failed

            // 1. Draw Fill (inset correctly by individual border lengths)
            if (hasVisibleFill && fillBrush is not null)
            {
                float fillX = pos.X + borderLeft;
                float fillY = pos.Y + borderTop;
                float fillWidth = Math.Max(0f, size.X - borderLeft - borderRight);
                float fillHeight = Math.Max(0f, size.Y - borderTop - borderBottom);

                if (fillWidth > 0 && fillHeight > 0)
                {
                    Rect fillBounds = new Rect(fillX, fillY, fillWidth, fillHeight);
                    renderTarget.FillRectangle(fillBounds, fillBrush);
                }
            }

            // 2. Draw Border (as four separate rectangles)
            if (hasVisibleBorder && borderBrush is not null)
            {
                // Top Border
                if (borderTop > 0)
                {
                    renderTarget.FillRectangle(new Rect(pos.X, pos.Y, size.X, borderTop), borderBrush);
                }
                // Bottom Border
                if (borderBottom > 0)
                {
                    renderTarget.FillRectangle(new Rect(pos.X, pos.Y + size.Y - borderBottom, size.X, borderBottom), borderBrush);
                }
                // Left Border (avoiding corners already drawn)
                if (borderLeft > 0)
                {
                    float leftHeight = Math.Max(0f, size.Y - borderTop - borderBottom);
                    if (leftHeight > 0)
                    {
                        renderTarget.FillRectangle(new Rect(pos.X, pos.Y + borderTop, borderLeft, leftHeight), borderBrush);
                    }
                }
                // Right Border (avoiding corners already drawn)
                if (borderRight > 0)
                {
                    float rightHeight = Math.Max(0f, size.Y - borderTop - borderBottom);
                    if (rightHeight > 0)
                    {
                        renderTarget.FillRectangle(new Rect(pos.X + size.X - borderRight, pos.Y + borderTop, borderRight, rightHeight), borderBrush);
                    }
                }
            }
        }
        finally
        {
            renderTarget.AntialiasMode = originalAntialiasMode; // Restore original AA mode
        }
    }
}