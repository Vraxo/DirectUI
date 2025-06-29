using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    public static bool Combobox(
        string id,
        ref int selectedIndex,
        string[] items,
        Vector2 size,
        Vector2 position = default,
        ButtonStylePack? theme = null,
        bool disabled = false)
    {
        if (!IsContextValid())
        {
            return false;
        }

        int intId = id.GetHashCode();
        Vector2 drawPos = Context.Layout.ApplyLayout(position);

        // Culling check
        if (!Context.Layout.IsRectVisible(new(drawPos.X, drawPos.Y, size.X, size.Y)))
        {
            Context.Layout.AdvanceLayout(size);
            return false;
        }

        var comboboxInstance = State.GetOrCreateElement<InternalComboboxLogic>(intId);
        int newIndex = comboboxInstance.UpdateAndDraw(intId, selectedIndex, items, drawPos, size, theme, disabled);

        bool valueChanged = newIndex != selectedIndex;
        if (valueChanged)
        {
            selectedIndex = newIndex;
        }

        Context.Layout.AdvanceLayout(size);
        return valueChanged;
    }
}