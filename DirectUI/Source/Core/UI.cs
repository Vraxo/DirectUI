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

public static partial class UI
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
}