using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    public static bool InputText(
        string id,
        ref string text,
        Vector2 size,
        Vector2 position = default,
        ButtonStylePack? theme = null,
        string placeholderText = "",
        bool isPassword = false,
        char passwordChar = '*',
        int maxLength = 1024,
        bool disabled = false,
        Vector2? textMargin = null)
    {
        if (!IsContextValid())
        {
            return false;
        }

        int intId = id.GetHashCode();
        Vector2 finalPosition = Context.Layout.ApplyLayout(position);
        Vector2 finalMargin = textMargin ?? new(4, 2);
        Rect widgetBounds = new(finalPosition.X, finalPosition.Y, size.X, size.Y);

        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(size);
            return false;
        }

        InputText lineEditInstance = State.GetOrCreateElement<InputText>(intId);
        var lineEditState = State.GetOrCreateElement<InputTextState>(HashCode.Combine(intId, "state"));

        bool textChanged = lineEditInstance.UpdateAndDraw(
            intId,
            ref text,
            lineEditState,
            finalPosition,
            size,
            theme,
            placeholderText,
            isPassword,
            passwordChar,
            maxLength,
            disabled,
            finalMargin);

        Context.Layout.AdvanceLayout(size);
        return textChanged;
    }
}