using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public enum StyleVar
{
    ItemSpacing,
    InnerItemSpacing,
    FramePadding,
    FrameRounding,
    FrameBorderSize,
    ButtonTextAlign,
}

public enum StyleColor
{
    Text,
    TextDisabled,
    Button,
    ButtonHovered,
    ButtonPressed,
    ButtonDisabled,
    Border,
    BorderHovered,
    BorderPressed,
    BorderDisabled,
    BorderFocused,
}

public static partial class UI
{
    // --- Style Stack API ---

    public static void PushStyleVar(StyleVar styleVar, float value)
    {
        Context.styleVarStack.Push((styleVar, value));
    }

    public static void PushStyleVar(StyleVar styleVar, Vector2 value)
    {
        Context.styleVarStack.Push((styleVar, value));
    }

    public static void PopStyleVar(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            if (Context.styleVarStack.Count > 0)
            {
                Context.styleVarStack.Pop();
            }
        }
    }

    public static void PushStyleColor(StyleColor styleColor, Color4 color)
    {
        Context.styleColorStack.Push((styleColor, color));
    }

    public static void PopStyleColor(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            if (Context.styleColorStack.Count > 0)
            {
                Context.styleColorStack.Pop();
            }
        }
    }

    // --- Style Accessors (for internal widget use) ---

    internal static T GetStyleVar<T>(StyleVar styleVar, T defaultValue)
    {
        foreach (var (key, value) in Context.styleVarStack)
        {
            if (key == styleVar && value is T typedValue)
            {
                return typedValue;
            }
        }
        return defaultValue;
    }

    internal static Color4 GetStyleColor(StyleColor styleColor, Color4 defaultValue)
    {
        foreach (var (key, value) in Context.styleColorStack)
        {
            if (key == styleColor)
            {
                return value;
            }
        }
        return defaultValue;
    }
}