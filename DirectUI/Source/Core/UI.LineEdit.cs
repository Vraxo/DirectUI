namespace DirectUI;

public static partial class UI
{
    public static bool LineEdit(string id, ref string text, LineEditDefinition definition)
    {
        if (!IsContextValid() || definition == null) return false;

        var lineEditInstance = State.GetOrCreateElement<LineEdit>(id);

        bool textChanged = lineEditInstance.UpdateAndDraw(id, ref text, definition);

        Context.Layout.AdvanceLayout(definition.Size);
        return textChanged;
    }
}