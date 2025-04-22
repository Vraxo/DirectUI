// MODIFIED: UI.cs
// Summary: Complete and unabbreviated class file. Implements the "Border Inside" simulation in DrawBoxStyleHelper for cleaner borders. Includes all previous features (containers, widgets, clipping, etc.).
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
        // Store context for the frame
        currentRenderTarget = context.RenderTarget;
        currentDWriteFactory = context.DWriteFactory;
        currentInputState = input;
        // Reset layout stack at the start of each frame
        containerStack.Clear();
    }

    public static void EndFrame()
    {
        // Check for layout mismatches
        if (containerStack.Count > 0)
        {
            Console.WriteLine($"Warning: Mismatch in Begin/End container calls. {containerStack.Count} containers left open at EndFrame.");
            containerStack.Clear(); // Clear on error to prevent issues next frame
        }
        // Release context references
        currentRenderTarget = null;
        currentDWriteFactory = null;
        // InputState is value type, no need to clear
    }

    // --- Containers ---
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
            Console.WriteLine("Error: EndHBoxContainer called without a matching BeginHBoxContainer."); return;
        }
        containerStack.Pop(); // Remove self from stack
        if (IsInLayoutContainer()) // Advance parent container's cursor if nested
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
        Vector2 startPosition = ApplyLayout(position); // Position relative to parent container or screen
        var containerState = new GridContainerState(id, startPosition, availableSize, numColumns, gap);
        containerStack.Push(containerState);
    }
    public static void EndGridContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not GridContainerState state)
        {
            Console.WriteLine("Error: EndGridContainer called without a matching BeginGridContainer."); return;
        }
        containerStack.Pop(); // Remove self
        if (IsInLayoutContainer()) // Advance parent if nested
        {
            // Finalize height calculation for the last row before getting total size
            if (state.RowHeights.Count > 0)
            {
                state.RowHeights[^1] = state.CurrentRowMaxHeight;
                // Add the height of the (now finished) last row to accumulated height
                state.AccumulatedHeight += state.CurrentRowMaxHeight;
                // Add gap if it wasn't the very first row
                if (state.RowHeights.Count > 1) // Check count *before* potentially adding gap
                {
                    state.AccumulatedHeight += state.Gap.Y;
                }
            }

            Vector2 containerSize = state.GetTotalOccupiedSize(); // Get size based on content
            AdvanceLayoutCursor(containerSize);
        }
    }

    // --- Widgets ---
    public static bool Button(string id, ButtonDefinition definition)
    {
        // Validate context and definition
        if (!IsContextValid() || definition is null) return false;

        // Get or create the internal button instance
        Button buttonInstance = GetOrCreateElement<Button>(id);

        // Determine position based on layout container or definition
        Vector2 elementPosition = GetCurrentLayoutPositionInternal();
        buttonInstance.Position = elementPosition;

        // Apply properties from definition to instance
        ApplyButtonDefinition(buttonInstance, definition);

        // --- Clipping for Grid ---
        bool pushedClip = false;
        Rect cellClipRect = Rect.Empty; // Store rect if needed elsewhere
        if (IsInLayoutContainer() && containerStack.Peek() is GridContainerState grid)
        {
            // Calculate clipping rectangle for the current cell
            float clipHeight = grid.StartPosition.Y + grid.AvailableSize.Y - grid.CurrentDrawPosition.Y; // Remaining height in grid
            cellClipRect = new Rect(
                grid.CurrentDrawPosition.X,
                grid.CurrentDrawPosition.Y,
                Math.Max(0f, grid.CellWidth),      // Use calculated cell width
                Math.Max(0f, clipHeight)           // Use remaining available height
            );
            // Ensure render target is not null before pushing clip
            if (CurrentRenderTarget is not null)
            {
                CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased);
                pushedClip = true;
            }
        }

        // --- Update State and Draw ---
        // Button.Update handles internal state, auto-width, drawing, and returns click status
        bool clicked = buttonInstance.Update();

        // --- Pop Clipping ---
        if (pushedClip && CurrentRenderTarget is not null)
        {
            CurrentRenderTarget.PopAxisAlignedClip();
        }

        // --- Advance Layout ---
        // Use the button's potentially modified size (due to AutoWidth)
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

        // --- Clipping for Grid ---
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

        // --- Update State and Draw ---
        float newValue = sliderInstance.UpdateAndDraw(CurrentInputState, GetCurrentDrawingContext(), currentValue);

        // --- Pop Clipping ---
        if (pushedClip && CurrentRenderTarget is not null)
        {
            CurrentRenderTarget.PopAxisAlignedClip();
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

        // --- Clipping for Grid ---
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

        // --- Update State and Draw ---
        float newValue = sliderInstance.UpdateAndDraw(CurrentInputState, GetCurrentDrawingContext(), currentValue);

        // --- Pop Clipping ---
        if (pushedClip && CurrentRenderTarget is not null)
        {
            CurrentRenderTarget.PopAxisAlignedClip();
        }

        // --- Advance Layout ---
        AdvanceLayout(sliderInstance.Size);

        return newValue;
    }


    // --- Helper Methods ---
    private static bool IsContextValid()
    {
        // Check if essential rendering resources are available
        if (CurrentRenderTarget is null || CurrentDWriteFactory is null)
        {
            Console.WriteLine($"Error: UI method called outside BeginFrame/EndFrame or context is invalid.");
            return false;
        }
        // Could add more checks here if needed (e.g., D2DFactory)
        return true;
    }
    private static DrawingContext GetCurrentDrawingContext()
    {
        // This method assumes IsContextValid() was called and returned true.
        // The non-null forgiveness operator (!) is used based on this assumption.
        // Ensure we only return if valid, otherwise behavior is undefined.
        if (!IsContextValid())
        {
            // This case should ideally not be reached if callers check IsContextValid first.
            // Throwing might be more appropriate than returning potentially invalid context.
            throw new InvalidOperationException("Attempted to get DrawingContext when UI context is invalid.");
        }
        return new DrawingContext(CurrentRenderTarget!, CurrentDWriteFactory!);
    }
    private static T GetOrCreateElement<T>(string id) where T : new()
    {
        // Retrieve existing element or create and store a new one
        if (uiElements.TryGetValue(id, out object? element) && element is T existingElement)
        {
            return existingElement;
        }
        else
        {
            // Console.WriteLine($"Creating new {typeof(T).Name} instance for ID: {id}"); // Optional logging
            T newElement = new();
            uiElements[id] = newElement; // Store the new element
            return newElement;
        }
    }
    private static void ApplyButtonDefinition(Button instance, ButtonDefinition definition)
    {
        // Apply properties from the definition to the button instance
        instance.Size = definition.Size; // Must be set before AutoWidth potentially changes it
        instance.Text = definition.Text;
        // Ensure themes are assigned, using existing or default if definition's is null
        instance.Themes = definition.Theme ?? instance.Themes ?? new ButtonStylePack();
        instance.Origin = definition.Origin ?? Vector2.Zero; // Use default if null
        instance.TextAlignment = definition.TextAlignment ?? new Alignment(HAlignment.Center, VAlignment.Center); // Use default if null
        instance.TextOffset = definition.TextOffset ?? Vector2.Zero; // Use default if null
        instance.AutoWidth = definition.AutoWidth;
        instance.TextMargin = definition.TextMargin ?? new Vector2(10, 5); // Use default if null
        instance.Behavior = definition.Behavior;
        instance.LeftClickActionMode = definition.LeftClickActionMode;
        instance.Disabled = definition.Disabled;
        instance.UserData = definition.UserData;
        // Note: Position is set by the layout system before this call
    }
    private static void ApplySliderDefinition(InternalSliderLogic instance, SliderDefinition definition)
    {
        // Apply properties from the definition to the slider instance
        instance.Size = definition.Size;
        instance.MinValue = definition.MinValue;
        instance.MaxValue = definition.MaxValue;
        instance.Step = definition.Step;
        instance.Theme = definition.Theme ?? instance.Theme ?? new SliderStyle();
        instance.GrabberTheme = definition.GrabberTheme ?? instance.GrabberTheme ?? new ButtonStylePack();
        instance.GrabberSize = definition.GrabberSize ?? instance.GrabberSize; // Use default if null
        instance.Origin = definition.Origin ?? Vector2.Zero;
        instance.Disabled = definition.Disabled;
        instance.UserData = definition.UserData;
        // Note: Position is set by the layout system before this call
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
        // If inside a container, advance its cursor based on the element's size.
        if (IsInLayoutContainer())
        {
            // Ensure element size is non-negative before advancing layout
            Vector2 nonNegativeSize = new Vector2(Math.Max(0, elementSize.X), Math.Max(0, elementSize.Y));
            AdvanceLayoutCursor(nonNegativeSize);
        }
    }
    private static bool IsInLayoutContainer() => containerStack.Count > 0;

    // Internal helper to get position without safety checks (assumes IsInLayoutContainer is true)
    private static Vector2 GetCurrentLayoutPositionInternal()
    {
        // No need for null check on Peek if IsInLayoutContainer is true
        object currentContainerState = containerStack.Peek();
        // Use pattern matching to get the correct position property
        return currentContainerState switch
        {
            HBoxContainerState hbox => hbox.CurrentPosition,
            VBoxContainerState vbox => vbox.CurrentPosition,
            GridContainerState grid => grid.CurrentDrawPosition, // Grid calculates draw position
            _ => Vector2.Zero, // Should not happen
        };
    }

    // Public version for external use if needed (e.g. drawing labels alongside elements)
    public static Vector2 GetCurrentLayoutPosition()
    {
        // Check if inside a container before calling the internal helper
        return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : Vector2.Zero;
    }


    private static void AdvanceLayoutCursor(Vector2 elementSize)
    {
        // Assumes IsInLayoutContainer was checked by the caller (AdvanceLayout)
        // Assumes elementSize is non-negative
        object currentContainerState = containerStack.Peek();

        // Update the container's state based on the type and element size
        switch (currentContainerState)
        {
            case HBoxContainerState hbox:
                // Track max height for the row
                if (elementSize.Y > hbox.MaxElementHeight) hbox.MaxElementHeight = elementSize.Y;
                // Calculate advance distance (add gap if not the first element)
                float advanceX = (hbox.AccumulatedWidth > 0 ? hbox.Gap : 0) + elementSize.X;
                // Update current position
                hbox.CurrentPosition = new Vector2(hbox.CurrentPosition.X + advanceX, hbox.CurrentPosition.Y);
                // Recalculate total accumulated width relative to the start
                hbox.AccumulatedWidth = hbox.CurrentPosition.X - hbox.StartPosition.X;
                break;

            case VBoxContainerState vbox:
                // Track max width for the column
                if (elementSize.X > vbox.MaxElementWidth) vbox.MaxElementWidth = elementSize.X;
                // Calculate advance distance (add gap if not the first element)
                float advanceY = (vbox.AccumulatedHeight > 0 ? vbox.Gap : 0) + elementSize.Y;
                // Update current position
                vbox.CurrentPosition = new Vector2(vbox.CurrentPosition.X, vbox.CurrentPosition.Y + advanceY);
                // Recalculate total accumulated height relative to the start
                vbox.AccumulatedHeight = vbox.CurrentPosition.Y - vbox.StartPosition.Y;
                break;

            case GridContainerState grid:
                // Grid state handles its own complex cursor/position movement
                grid.MoveToNextCell(elementSize);
                break;
            default:
                // Should not happen
                Console.WriteLine("Error: AdvanceLayoutCursor called with unexpected container type.");
                break;
        }
    }
    // --- End Layout Helpers ---


    // --- Brush Cache ---
    public static ID2D1SolidColorBrush GetOrCreateBrush(Color4 color)
    {
        // Ensure render target is available before proceeding
        if (CurrentRenderTarget is null)
        {
            Console.WriteLine("Error: GetOrCreateBrush called with no active render target.");
            return null!; // Or throw exception
        }

        // Check cache first
        if (brushCache.TryGetValue(color, out ID2D1SolidColorBrush? brush) && brush is not null)
        {
            // Assume cached brush is valid. Rely on CleanupResources for device loss.
            return brush;
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
                Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}");
                return null!; // Indicate failure to create brush.
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). External cleanup needed.");
            return null!; // Indicate failure.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating brush for color {color}: {ex.Message}");
            return null!; // Indicate failure.
        }
    }


    // --- Resource Cleanup ---
    public static void CleanupResources()
    {
        Console.WriteLine("UI Resource Cleanup: Disposing cached brushes...");
        int count = brushCache.Count;
        foreach (var pair in brushCache)
        {
            pair.Value?.Dispose(); // Ensure value is not null before disposing
        }
        brushCache.Clear(); // Clear the dictionary
        Console.WriteLine($"UI Resource Cleanup finished. Disposed {count} brushes.");

        // Clear layout stack state
        containerStack.Clear();

        // Do not clear uiElements - they hold persistent state between frames.
    }


    // --- SHARED DRAWING HELPERS ---

    /// <summary>
    /// Draws a BoxStyle using an inset fill and border drawn inside the outer bounds
    /// to prevent edge artifacts.
    /// </summary>
    internal static void DrawBoxStyleHelper(ID2D1RenderTarget renderTarget, Vector2 pos, Vector2 size, BoxStyle style)
    {
        // Basic validation
        if (renderTarget is null || style is null || size.X <= 0 || size.Y <= 0)
        {
            return; // Nothing to draw
        }

        Rect outerBounds = new Rect(pos.X, pos.Y, size.X, size.Y);
        // Use the shared GetOrCreateBrush method which handles caching and errors
        ID2D1SolidColorBrush fillBrush = GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush borderBrush = GetOrCreateBrush(style.BorderColor);

        // Use actual thickness, ensuring it's not negative
        float borderThickness = Math.Max(0f, style.BorderThickness);
        // Determine visibility based on alpha and brush validity
        bool hasVisibleBorder = borderThickness > 0 && style.BorderColor.A > 0 && borderBrush is not null;
        bool hasVisibleFill = style.FillColor.A > 0 && fillBrush is not null;

        // Short circuit if nothing is visible
        if (!hasVisibleFill && !hasVisibleBorder) return;

        // Calculate inset amount
        float halfBorder = borderThickness / 2.0f;

        // --- Rounded Corners ---
        if (style.Roundness > 0.0f)
        {
            // Calculate outer radius for the border path based on minimum dimension
            float halfWidth = outerBounds.Width * 0.5f;
            float halfHeight = outerBounds.Height * 0.5f;
            float maxOuterRadius = Math.Min(halfWidth, halfHeight);
            // Clamp roundness factor and calculate final radius
            float outerRadius = Math.Max(0f, maxOuterRadius * Math.Clamp(style.Roundness, 0.0f, 1.0f));

            // Check if outer radius is valid
            if (float.IsFinite(outerRadius) && outerRadius >= 0)
            {
                // 1. Draw Fill (inset by full border thickness)
                if (hasVisibleFill)
                {
                    // Calculate fill area bounds, inset by half border from each side
                    // Ensure inset dimensions don't go below zero
                    float fillInsetWidth = Math.Max(0f, outerBounds.Width - borderThickness);
                    float fillInsetHeight = Math.Max(0f, outerBounds.Height - borderThickness);

                    if (fillInsetWidth > 0 && fillInsetHeight > 0) // Only draw fill if it has positive size
                    {
                        Rect fillBounds = new Rect(outerBounds.X + halfBorder, outerBounds.Y + halfBorder, fillInsetWidth, fillInsetHeight);
                        // Fill radius is reduced by half border thickness, clamped at 0
                        float fillRadius = Math.Max(0f, outerRadius - halfBorder);

                        System.Drawing.RectangleF fillRectF = new(fillBounds.X, fillBounds.Y, fillBounds.Width, fillBounds.Height);
                        RoundedRectangle fillRoundedRect = new(fillRectF, fillRadius, fillRadius);
                        renderTarget.FillRoundedRectangle(fillRoundedRect, fillBrush);
                    }
                    // Else: If border is thicker than shape, fill is completely covered, do nothing.
                }

                // 2. Draw Border (path is centered on a path inset by half border thickness)
                if (hasVisibleBorder)
                {
                    // Calculate the path for the border stroke (inset by half thickness)
                    float borderPathWidth = Math.Max(0f, outerBounds.Width - borderThickness);
                    float borderPathHeight = Math.Max(0f, outerBounds.Height - borderThickness);

                    // Only draw if the path itself has size (otherwise border fills everything)
                    if (borderPathWidth > 0 && borderPathHeight > 0)
                    {
                        Rect borderPathBounds = new Rect(outerBounds.X + halfBorder, outerBounds.Y + halfBorder, borderPathWidth, borderPathHeight);
                        // Radius for the border path is also reduced by half thickness, clamped at 0
                        float borderPathRadius = Math.Max(0f, outerRadius - halfBorder);

                        System.Drawing.RectangleF borderPathRectF = new(borderPathBounds.X, borderPathBounds.Y, borderPathBounds.Width, borderPathBounds.Height);
                        RoundedRectangle borderPathRoundedRect = new(borderPathRectF, borderPathRadius, borderPathRadius);

                        // Draw the stroke along this inset path
                        renderTarget.DrawRoundedRectangle(borderPathRoundedRect, borderBrush, borderThickness);
                    }
                    else // Border is thicker than the shape itself
                    {
                        // If there's no fill, fill the entire outer bounds with the border color.
                        // If there is a fill, it's already covered, so do nothing extra.
                        if (!hasVisibleFill)
                        {
                            // Need to use the outer rounded path for filling if corners are rounded
                            System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                            RoundedRectangle outerRoundedRect = new(outerRectF, outerRadius, outerRadius);
                            renderTarget.FillRoundedRectangle(outerRoundedRect, borderBrush);
                        }
                    }
                }
                return; // Finished rounded drawing
            }
            else // Fallback if radius calculation failed
            {
                Console.WriteLine("Warning: Invalid radius calculated for rounded rectangle. Falling back to sharp.");
                // Fall through to sharp corner logic below
            }
        }

        // --- Sharp Corners ("Border Inside" Approach) ---
        // This path is taken if Roundness <= 0 or if rounded calculation failed

        // 1. Draw Fill (inset by full border thickness)
        if (hasVisibleFill)
        {
            float fillInsetWidth = Math.Max(0f, outerBounds.Width - borderThickness);
            float fillInsetHeight = Math.Max(0f, outerBounds.Height - borderThickness);
            if (fillInsetWidth > 0 && fillInsetHeight > 0)
            {
                Rect fillBounds = new Rect(outerBounds.X + halfBorder, outerBounds.Y + halfBorder, fillInsetWidth, fillInsetHeight);
                renderTarget.FillRectangle(fillBounds, fillBrush);
            }
            // Else: Border is thicker than shape, fill is covered.
        }

        // 2. Draw Border (path inset by half border thickness)
        if (hasVisibleBorder)
        {
            // Calculate the path for the border stroke
            float borderPathWidth = Math.Max(0f, outerBounds.Width - borderThickness);
            float borderPathHeight = Math.Max(0f, outerBounds.Height - borderThickness);
            if (borderPathWidth > 0 && borderPathHeight > 0) // Draw stroke if path exists
            {
                Rect borderPathBounds = new Rect(outerBounds.X + halfBorder, outerBounds.Y + halfBorder, borderPathWidth, borderPathHeight);
                renderTarget.DrawRectangle(borderPathBounds, borderBrush, borderThickness);
            }
            else // Border is thicker than the shape
            {
                // If no fill, fill the whole outer bounds with border color
                if (!hasVisibleFill)
                {
                    renderTarget.FillRectangle(outerBounds, borderBrush);
                }
            }
        }
    }


    /// <summary>
    /// Helper specifically for drawing sharp-cornered rectangles. THIS IS NOW OBSOLETE
    /// as the main DrawBoxStyleHelper handles sharp corners with inset fill.
    /// Can be safely removed.
    /// </summary>
    private static void DrawSharpRectangleHelper(ID2D1RenderTarget renderTarget, Rect bounds, bool canFill, ID2D1SolidColorBrush? fillBrush, bool canDrawBorder, ID2D1SolidColorBrush? borderBrush, float borderThickness)
    {
        // Obsolete: Logic moved into DrawBoxStyleHelper's sharp path.
    }
}