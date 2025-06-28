namespace DirectUI;

public static partial class UI
{
    /// <summary>
    /// Renders an editable text input box.
    /// </summary>
    /// <param name="id">A unique identifier for the control.</param>
    /// <param name="text">The string to be displayed and edited. This value is modified by the control.</param>
    /// <param name="definition">The definition object describing the control's appearance and behavior.</param>
    /// <returns>True if the text was changed during this frame; otherwise, false.</returns>
    public static bool LineEdit(string id, ref string text, LineEditDefinition definition)
    {
        if (!IsContextValid() || definition == null) return false;

        var lineEditInstance = State.GetOrCreateElement<LineEdit>(id);

        bool textChanged = lineEditInstance.UpdateAndDraw(id, ref text, definition);

        Context.AdvanceLayout(definition.Size);
        return textChanged;
    }
}