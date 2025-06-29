using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Xml.Linq;
using Cherris; // Reference the Cherris engine project
using Vortice.DirectWrite;

namespace DirectUI;

public class InspectorView
{
    private const float PanelPadding = 10f;

    private readonly ButtonStyle _titleStyle = new()
    {
        FontWeight = FontWeight.SemiBold,
        FontSize = 16f
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
            var linePos = UI.Context.Layout.GetCurrentPosition() + new Vector2(0, -4);
            UI.Context.RenderTarget.DrawLine(linePos, linePos + new Vector2(availableWidth, 0), UI.Resources.GetOrCreateBrush(UI.Context.RenderTarget, DefaultTheme.NormalBorder), 1f);

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
                    availableSize: new Vector2(availableWidth, 24),
                    numColumns: 2,
                    gap: new Vector2(gridGap, 0)
                );

                try
                {
                    // Column 1: Property Name Label
                    UI.Label(
                        $"prop_label_{prop.Name}",
                        prop.Name,
                        size: new(labelCellWidth, 24),
                        textAlignment: new(HAlignment.Left, VAlignment.Center)
                    );

                    // Column 2: Property Value (as a simple label for now)
                    object? value = prop.GetValue(selectedNode, null);
                    string valueString;

                    // Custom formatting for common types
                    switch (value)
                    {
                        case Vector2 v: valueString = $"X:{v.X:F2}, Y:{v.Y:F2}"; break;
                        case bool b: valueString = b.ToString(); break;
                        case null: valueString = "null"; break;
                        default: valueString = value.ToString() ?? "null"; break;
                    }

                    // A disabled button makes a good-looking, non-interactive label field
                    UI.Button($"prop_value_{prop.Name}", valueString, size: new(valueCellWidth, 24), disabled: true);

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