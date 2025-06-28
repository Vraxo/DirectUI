using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    public static bool LineEdit(
        int id,
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
        if (!IsContextValid()) return false;

        var lineEditInstance = State.GetOrCreateElement<LineEdit>(id);

        var finalPosition = Context.Layout.ApplyLayout(position);
        var finalMargin = textMargin ?? new Vector2(4, 2);

        bool textChanged = lineEditInstance.UpdateAndDraw(
            id,
            ref text,
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