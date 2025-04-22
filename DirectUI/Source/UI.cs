// MODIFIED: UI.cs
// Summary: Updated DrawBoxStyleHelper to handle individual BorderLengthTop/Right/Bottom/Left properties. Sharp corners are drawn accurately with potentially different border sizes. Rounded corners approximate by averaging border sizes.
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


    // --- SHARED DRAWING HELPERS ---

    /// <summary>
    /// Draws a BoxStyle, supporting independent border lengths for sharp corners.
    /// For rounded corners, it approximates by averaging horizontal/vertical border lengths.
    /// </summary>
    internal static void DrawBoxStyleHelper(ID2D1RenderTarget renderTarget, Vector2 pos, Vector2 size, BoxStyle style)
    {
        if (renderTarget is null || style is null || size.X <= 0 || size.Y <= 0)
        {
            return; // Nothing to draw
        }

        // Get brushes
        ID2D1SolidColorBrush fillBrush = GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush borderBrush = GetOrCreateBrush(style.BorderColor);

        // Check visibility
        bool hasVisibleFill = style.FillColor.A > 0 && fillBrush is not null;
        bool hasVisibleBorder = style.BorderColor.A > 0 && borderBrush is not null &&
                                (style.BorderLengthTop > 0 || style.BorderLengthRight > 0 ||
                                 style.BorderLengthBottom > 0 || style.BorderLengthLeft > 0);

        if (!hasVisibleFill && !hasVisibleBorder) return;

        // Ensure border lengths are non-negative
        float borderTop = Math.Max(0f, style.BorderLengthTop);
        float borderRight = Math.Max(0f, style.BorderLengthRight);
        float borderBottom = Math.Max(0f, style.BorderLengthBottom);
        float borderLeft = Math.Max(0f, style.BorderLengthLeft);

        // --- Rounded Corners ---
        if (style.Roundness > 0.0f)
        {
            // --- Approximation for Rounded Corners with Independent Borders ---
            // Calculate average horizontal and vertical border thicknesses for the inset/stroke logic
            float avgHorizontalBorder = (borderLeft + borderRight) / 2.0f;
            float avgVerticalBorder = (borderTop + borderBottom) / 2.0f;
            // Use the maximum of the two averages for a single stroke width approximation
            float approxBorderThickness = Math.Max(avgHorizontalBorder, avgVerticalBorder);
            float halfApproxBorder = approxBorderThickness / 2.0f;

            // Calculate outer bounds and radius
            Rect outerBounds = new Rect(pos.X, pos.Y, size.X, size.Y);
            float maxOuterRadius = Math.Min(outerBounds.Width * 0.5f, outerBounds.Height * 0.5f);
            float outerRadius = Math.Max(0f, maxOuterRadius * Math.Clamp(style.Roundness, 0.0f, 1.0f));

            if (float.IsFinite(outerRadius) && outerRadius >= 0)
            {
                // 1. Draw Fill (inset by the approximated half border thickness)
                if (hasVisibleFill)
                {
                    float fillInsetWidth = Math.Max(0f, outerBounds.Width - approxBorderThickness);
                    float fillInsetHeight = Math.Max(0f, outerBounds.Height - approxBorderThickness);

                    if (fillInsetWidth > 0 && fillInsetHeight > 0)
                    {
                        Rect fillBounds = new Rect(outerBounds.X + halfApproxBorder, outerBounds.Y + halfApproxBorder, fillInsetWidth, fillInsetHeight);
                        float fillRadius = Math.Max(0f, outerRadius - halfApproxBorder);

                        System.Drawing.RectangleF fillRectF = new(fillBounds.X, fillBounds.Y, fillBounds.Width, fillBounds.Height);
                        RoundedRectangle fillRoundedRect = new(fillRectF, fillRadius, fillRadius);
                        renderTarget.FillRoundedRectangle(fillRoundedRect, fillBrush);
                    }
                }

                // 2. Draw Border (using approximated thickness and inset)
                if (hasVisibleBorder) // Check if border should be drawn at all
                {
                    float borderPathWidth = Math.Max(0f, outerBounds.Width - approxBorderThickness);
                    float borderPathHeight = Math.Max(0f, outerBounds.Height - approxBorderThickness);

                    if (borderPathWidth > 0 && borderPathHeight > 0)
                    {
                        Rect borderPathBounds = new Rect(outerBounds.X + halfApproxBorder, outerBounds.Y + halfApproxBorder, borderPathWidth, borderPathHeight);
                        float borderPathRadius = Math.Max(0f, outerRadius - halfApproxBorder);

                        System.Drawing.RectangleF borderPathRectF = new(borderPathBounds.X, borderPathBounds.Y, borderPathBounds.Width, borderPathBounds.Height);
                        RoundedRectangle borderPathRoundedRect = new(borderPathRectF, borderPathRadius, borderPathRadius);
                        renderTarget.DrawRoundedRectangle(borderPathRoundedRect, borderBrush, approxBorderThickness);
                    }
                    else if (!hasVisibleFill) // Border thicker than shape, fill outer bounds if no inner fill
                    {
                        System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                        RoundedRectangle outerRoundedRect = new(outerRectF, outerRadius, outerRadius);
                        renderTarget.FillRoundedRectangle(outerRoundedRect, borderBrush);
                    }
                }
                return; // Finished rounded (approximated) drawing
            }
            // Fallback to sharp if radius calculation failed
        }

        // --- Sharp Corners (Accurate Independent Borders) ---
        // This path is taken if Roundness <= 0 or if rounded calculation failed

        // 1. Draw Fill (inset correctly by individual border lengths)
        if (hasVisibleFill)
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
            // else: Fill area is completely covered by borders.
        }

        // 2. Draw Border (as four separate rectangles)
        if (hasVisibleBorder)
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
}