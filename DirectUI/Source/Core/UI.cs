using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

internal class TreeViewState
{
    internal string Id { get; }
    internal TreeStyle Style { get; }
    internal Stack<bool> IndentLineState { get; } = new();

    internal TreeViewState(string id, TreeStyle style)
    {
        Id = id;
        Style = style;
    }
}


public enum HSliderDirection { LeftToRight, RightToLeft }
public enum VSliderDirection { TopToBottom, BottomToTop }

public static partial class UI
{
    // --- Font Caching Key ---
    private readonly struct FontKey : IEquatable<FontKey>
    {
        public readonly string FontName;
        public readonly float FontSize;
        public readonly FontWeight FontWeight;
        public readonly FontStyle FontStyle;
        public readonly FontStretch FontStretch;

        public FontKey(ButtonStyle style)
        {
            FontName = style.FontName;
            FontSize = style.FontSize;
            FontWeight = style.FontWeight;
            FontStyle = style.FontStyle;
            FontStretch = style.FontStretch;
        }

        public bool Equals(FontKey other)
        {
            return FontName == other.FontName &&
                   FontSize.Equals(other.FontSize) &&
                   FontWeight == other.FontWeight &&
                   FontStyle == other.FontStyle &&
                   FontStretch == other.FontStretch;
        }

        public override bool Equals(object? obj) => obj is FontKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(FontName, FontSize, FontWeight, FontStyle, FontStretch);
    }


    // --- State fields ---
    private static ID2D1HwndRenderTarget? currentRenderTarget;
    private static IDWriteFactory? currentDWriteFactory;
    private static InputState currentInputState;
    private static readonly Dictionary<string, object> uiElements = new();
    private static readonly Dictionary<Color4, ID2D1SolidColorBrush> brushCache = new();
    private static readonly Dictionary<FontKey, IDWriteTextFormat> textFormatCache = new();
    private static readonly Stack<object> containerStack = new();
    private static readonly Stack<TreeViewState> treeStateStack = new();

    // Input State
    private static bool captureAttemptedThisFrame = false;
    private static int inputCaptorId = 0;
    private static int potentialInputTargetId = 0;
    private static int activelyPressedElementId = 0;
    internal static bool dragInProgressFromPreviousFrame = false;
    internal static bool nonSliderElementClaimedPress = false;

    // --- Public/Internal Properties ---
    public static ID2D1HwndRenderTarget? CurrentRenderTarget => currentRenderTarget;
    public static IDWriteFactory? CurrentDWriteFactory => currentDWriteFactory;
    public static InputState CurrentInputState => currentInputState;
    public static int ActivelyPressedElementId => activelyPressedElementId;
    public static int InputCaptorId => inputCaptorId;
    internal static int PotentialInputTargetId => potentialInputTargetId;

    // --- Frame Management ---
    public static void BeginFrame(DrawingContext context, InputState input)
    {
        currentRenderTarget = context.RenderTarget;
        currentDWriteFactory = context.DWriteFactory;
        currentInputState = input;
        containerStack.Clear();
        treeStateStack.Clear();

        dragInProgressFromPreviousFrame = input.IsLeftMouseDown && activelyPressedElementId != 0;

        captureAttemptedThisFrame = false;
        inputCaptorId = 0;
        potentialInputTargetId = 0;
        nonSliderElementClaimedPress = false;

        // The active element is now cleared by the element itself upon release,
        // or by a new element capturing input. This prevents the state from being
        // cleared before the release event can be processed.
    }

    public static void EndFrame()
    {
        if (containerStack.Count > 0)
        {
            Console.WriteLine($"Warning: Mismatch in Begin/End container calls. {containerStack.Count} containers left open at EndFrame.");
            containerStack.Clear();
        }
        if (treeStateStack.Count > 0)
        {
            Console.WriteLine($"Warning: Mismatch in Begin/End Tree calls. {treeStateStack.Count} trees left open at EndFrame.");
            treeStateStack.Clear();
        }
        currentRenderTarget = null;
        currentDWriteFactory = null;
    }

    internal static void BeginTree(string id, TreeStyle style)
    {
        treeStateStack.Push(new TreeViewState(id, style));
    }

    internal static void EndTree()
    {
        if (treeStateStack.Count == 0)
        {
            Console.WriteLine("Error: EndTree called without a matching BeginTree.");
            return;
        }
        treeStateStack.Pop();
    }


    // --- Input Capture & Targeting ---

    public static bool IsElementActive()
    {
        return activelyPressedElementId is not 0;
    }

    internal static void SetPotentialInputTarget(int id)
    {
        potentialInputTargetId = id;
    }

    internal static void SetPotentialCaptorForFrame(int id)
    {
        captureAttemptedThisFrame = true;
        inputCaptorId = id;
        activelyPressedElementId = id;
    }

    internal static void SetButtonPotentialCaptorForFrame(int id)
    {
        captureAttemptedThisFrame = true;
        inputCaptorId = id;
        activelyPressedElementId = id;
        nonSliderElementClaimedPress = true;
    }

    internal static void ClearActivePress(int id)
    {
        if (activelyPressedElementId == id)
        {
            activelyPressedElementId = 0;
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

    // --- Brush and Font Cache ---
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

    public static IDWriteTextFormat? GetOrCreateTextFormat(ButtonStyle style)
    {
        if (CurrentDWriteFactory is null) return null;

        var key = new FontKey(style);
        if (textFormatCache.TryGetValue(key, out var format))
        {
            return format;
        }

        try
        {
            var newFormat = CurrentDWriteFactory.CreateTextFormat(
                style.FontName,
                null,
                style.FontWeight,
                style.FontStyle,
                style.FontStretch,
                style.FontSize,
                "en-us"
            );
            if (newFormat is not null)
            {
                textFormatCache[key] = newFormat;
            }
            return newFormat;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating text format for font '{style.FontName}': {ex.Message}");
            return null;
        }
    }

    public static Vector2 MeasureText(IDWriteFactory dwriteFactory, string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text) || style is null)
        {
            return Vector2.Zero;
        }

        IDWriteTextFormat? textFormat = GetOrCreateTextFormat(style);
        if (textFormat is null)
        {
            Console.WriteLine("Warning: Failed to create/get TextFormat for measurement.");
            return Vector2.Zero;
        }

        using var textLayout = dwriteFactory.CreateTextLayout(
            text,
            textFormat,
            float.MaxValue, // Max width
            float.MaxValue  // Max height
        );

        TextMetrics textMetrics = textLayout.Metrics;
        return new Vector2(textMetrics.WidthIncludingTrailingWhitespace, textMetrics.Height);
    }


    // --- Resource Cleanup ---
    public static void CleanupResources()
    {
        Console.WriteLine("UI Resource Cleanup: Disposing cached brushes and text formats...");
        int brushCount = brushCache.Count;
        foreach (var pair in brushCache) { pair.Value?.Dispose(); }
        brushCache.Clear();

        int formatCount = textFormatCache.Count;
        foreach (var pair in textFormatCache) { pair.Value?.Dispose(); }
        textFormatCache.Clear();

        Console.WriteLine($"UI Resource Cleanup finished. Disposed {brushCount} brushes and {formatCount} text formats.");
        containerStack.Clear();
        treeStateStack.Clear();
    }
}