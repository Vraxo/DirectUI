using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    public static bool LineEdit(
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
        if (!IsContextValid()) return false;

        int intId = id.GetHashCode();
        var finalPosition = Context.Layout.ApplyLayout(position);
        var finalMargin = textMargin ?? new Vector2(4, 2);

        // Culling Check
        Rect widgetBounds = new Rect(finalPosition.X, finalPosition.Y, size.X, size.Y);
        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(size); // Still advance layout cursor
            return false;
        }

        // Get the stateless logic/drawing class instance
        var lineEditInstance = State.GetOrCreateElement<LineEdit>(intId);
        // Get the state object for this specific line edit
        var lineEditState = State.GetOrCreateElement<LineEditState>(HashCode.Combine(intId, "state"));

        bool textChanged = lineEditInstance.UpdateAndDraw(
            intId,
            ref text,
            lineEditState, // Pass the state object to the logic class
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