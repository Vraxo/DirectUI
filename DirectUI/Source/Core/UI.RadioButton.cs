using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    public static bool RadioButtons(
        string id,
        string[] labels,
        ref int activeIndex,
        ButtonStylePack? theme = null,
        Vector2 size = default,
        bool autoWidth = false,
        float gap = 10f)
    {
        if (!IsContextValid() || labels == null || labels.Length == 0)
        {
            return false;
        }

        bool valueChanged = false;

        BeginHBoxContainer(id + "_hbox", Context.Layout.GetCurrentPosition(), gap);

        for (int i = 0; i < labels.Length; i++)
        {
            // Use isActive parameter of the Button to control visual state.
            if (Button(
                    id: id + "_btn_" + i,
                    text: labels[i],
                    isActive: i == activeIndex,
                    theme: theme,
                    size: size,
                    autoWidth: autoWidth
                    ))
            {
                if (activeIndex != i)
                {
                    activeIndex = i;
                    valueChanged = true;
                }
            }
        }

        EndHBoxContainer();

        return valueChanged;
    }
}