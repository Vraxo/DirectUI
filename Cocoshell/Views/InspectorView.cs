using System.Numerics;
using System.Reflection;
using Cherris;
using Vortice.DirectWrite;

namespace DirectUI;

public class InspectorView
{
    private const float PanelPadding = 10f;

    private readonly ButtonStyle _titleStyle = new()
    {
        FontWeight = FontWeight.SemiBold,
        FontSize = 14f
    };

    private readonly ButtonStyle _propertyLabelStyle = new()
    {
        FontWeight = FontWeight.SemiBold,
        FontColor = new(0.8f, 0.8f, 0.8f, 1.0f)
    };

    public void Draw(Node? selectedNode, float panelWidth)
    {
        float availableWidth = panelWidth - (PanelPadding * 2);

        UI.Label(
            "inspector_title",
            "Inspector",
            size: new(availableWidth, 0),
            style: _titleStyle,
            textAlignment: new(HAlignment.Center, VAlignment.Center)
        );

        if (selectedNode is null)
        {
            UI.Button("no_selection_label", "No node selected.", disabled: true, autoWidth: true);
            return;
        }

        // --- Layout container for all properties ---
        UI.BeginVBoxContainer("inspector_properties_vbox", UI.Context.Layout.GetCurrentPosition(), 8f);
        {
            // --- Node Type Display ---
            UI.Label("type_header", $"Type: {selectedNode.GetType().Name}", style: _propertyLabelStyle);

            // --- Separator ---
            Vector2 linePos = UI.Context.Layout.GetCurrentPosition() + new Vector2(0, -4);

            UI.Context.RenderTarget.DrawLine(
                linePos,
                linePos + new Vector2(availableWidth, 0),
                UI.Resources.GetOrCreateBrush(UI.Context.RenderTarget, DefaultTheme.NormalBorder),
                1f);

            // --- Property Editors/Viewers ---
            float gridGap = 8f;
            float labelCellWidth = (availableWidth - gridGap) * 0.4f; // 40% for label
            float valueCellWidth = (availableWidth - gridGap) * 0.6f; // 60% for value

            // Get all public instance properties that can be read and are not indexers
            var properties = selectedNode.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

            foreach (var prop in properties)
            {
                // Skip some properties that aren't useful to display this way
                if (prop.Name is "Parent" or "Children" or "AbsolutePath" or "ScaledSize")
                    continue;

                UI.BeginGridContainer(
                    id: $"grid_prop_{selectedNode.Name}_{prop.Name}",
                    position: UI.Context.Layout.GetCurrentPosition(),
                    availableSize: new(availableWidth, 24),
                    numColumns: 2,
                    gap: new(gridGap, 0)
                );

                try
                {
                    UI.Label(
                        $"prop_label_{prop.Name}",
                        prop.Name,
                        size: new(labelCellWidth, 24),
                        textAlignment: new(HAlignment.Left, VAlignment.Center)
                    );

                    object? value = prop.GetValue(selectedNode, null);

                    // Use Checkbox for writable boolean properties
                    if (prop.PropertyType == typeof(bool) && prop.CanWrite)
                    {
                        bool isChecked = (bool)(value ?? false);
                        // The label is in the first column, so pass empty string here.
                        if (UI.Checkbox($"prop_value_edit_{prop.Name}", "", ref isChecked))
                        {
                            prop.SetValue(selectedNode, isChecked);
                            // A more robust system would flag the scene as dirty
                        }
                    }
                    else // For all other properties (including readonly bools), display as text
                    {
                        string valueString = value switch
                        {
                            Vector2 v => $"X:{v.X:F2}, Y:{v.Y:F2}",
                            bool b => b.ToString(),
                            null => "null",
                            _ => value.ToString() ?? "null",
                        };

                        UI.Button($"prop_value_display_{prop.Name}", valueString, size: new(valueCellWidth, 24), disabled: true);
                    }
                }
                catch (Exception ex)
                {
                    UI.Label($"prop_error_{prop.Name}", $"Error: {ex.Message}");
                }

                UI.EndGridContainer();
            }
        }
        UI.EndVBoxContainer();
    }
}