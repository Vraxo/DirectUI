using System.Numerics;
using System.Reflection;
using System.Text;
using Cherris;
using Vortice.DirectWrite;

namespace DirectUI;

public class InspectorView
{
    private const float PanelGap = 10f;
    private const float PanelPadding = 10f;
    private const float GridGap = 8f;

    private readonly ButtonStyle _titleStyle = new()
    {
        FontWeight = FontWeight.SemiBold,
        FontSize = 28f
    };

    private readonly ButtonStyle _propertyLabelStyle = new()
    {
        FontSize = 13f,
        FontWeight = FontWeight.Normal,
        FontColor = new(0.8f, 0.8f, 0.8f, 1.0f)
    };

    private class Vector2EditState
    {
        public string X { get; set; } = "0";
        public string Y { get; set; } = "0";
    }

    public void Draw(Node? selectedNode, float panelWidth, float panelHeight)
    {
        float availableContentWidth = panelWidth - (PanelPadding * 2);
        var startPos = UI.Context.Layout.GetCurrentPosition();

        UI.BeginVBoxContainer("inspector_outer_vbox", startPos, PanelGap);
        {
            DrawHeader(availableContentWidth);
            UI.Separator(availableContentWidth, thickness: 1f, verticalPadding: 4f);

            var currentPos = UI.Context.Layout.GetCurrentPosition();
            var heightUsed = currentPos.Y - startPos.Y;
            var scrollableHeight = panelHeight - heightUsed;


            if (scrollableHeight < 0)
            {
                scrollableHeight = 0;
            }

            Vector2 scrollableSize = new(availableContentWidth, scrollableHeight);

            UI.BeginScrollableRegion("inspector_scroll", scrollableSize, out float innerContentWidth);
            {
                if (selectedNode is null)
                {
                    UI.BeginVBoxContainer("inspector_content_vbox", UI.Context.Layout.GetCurrentPosition(), 8f);
                    UI.Button("no_selection_label", "No node selected.", disabled: true, autoWidth: true);
                    UI.EndVBoxContainer();
                }
                else
                {
                    UI.BeginVBoxContainer("inspector_properties_vbox", UI.Context.Layout.GetCurrentPosition(), 8f);
                    {
                        DrawAllProperties(selectedNode, innerContentWidth);
                    }
                    UI.EndVBoxContainer();
                }
            }
            UI.EndScrollableRegion();
        }
        UI.EndVBoxContainer();
    }

    private void DrawHeader(float availableWidth)
    {
        UI.Text(
            "inspector_title",
            "Inspector",
            size: new(availableWidth, 0),
            style: _titleStyle,
            textAlignment: new(HAlignment.Center, VAlignment.Center)
        );
    }

    private void DrawAllProperties(Node selectedNode, float availableWidth)
    {
        Type? currentType = selectedNode.GetType();
        bool isFirstGroup = true;

        var classHeaderStyle = new ButtonStyle
        {
            FontWeight = FontWeight.Bold,
            FontSize = 13f
        };

        while (currentType != null && typeof(Node).IsAssignableFrom(currentType))
        {
            var properties = currentType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.CanRead &&
                            p.GetIndexParameters().Length == 0 &&
                            !p.IsDefined(typeof(HideFromInspectorAttribute), false))
                .ToList();

            if (properties.Any())
            {
                if (!isFirstGroup)
                {
                    // Add separator BEFORE the group, but not for the first one.
                    UI.Separator(availableWidth, thickness: 1f, verticalPadding: 8f);
                }

                UI.Text(
                    $"header_{currentType.Name}",
                    currentType.Name,
                    style: classHeaderStyle,
                    size: new(availableWidth, 0),
                    textAlignment: new Alignment(HAlignment.Center, VAlignment.Center)
                );


                foreach (var prop in properties)
                {
                    DrawPropertyRow(selectedNode, prop, availableWidth);
                }

                isFirstGroup = false;
            }

            currentType = currentType.BaseType;
        }
    }

    private void DrawPropertyRow(Node node, PropertyInfo prop, float availableWidth)
    {
        float labelWidth = (availableWidth - GridGap) * 0.4f;
        float editorWidth = (availableWidth - GridGap) * 0.6f;
        string hboxId = $"hbox_prop_{node.Name}_{prop.Name}";
        object? value;

        try
        {
            value = prop.GetValue(node, null);
        }
        catch (Exception ex)
        {
            UI.Text($"prop_error_{prop.Name}", $"Error getting value: {ex.Message}");
            return;
        }

        UI.BeginHBoxContainer(hboxId, UI.Context.Layout.GetCurrentPosition(), GridGap);

        try
        {
            UI.Text(
                $"prop_label_{prop.Name}",
                SplitPascalCase(prop.Name),
                size: new(labelWidth, 24),
                style: _propertyLabelStyle,
                textAlignment: new(HAlignment.Left, VAlignment.Center)
            );

            DrawPropertyEditor(node, prop, value, editorWidth);
        }
        finally
        {
            UI.EndHBoxContainer();
        }
    }

    private static void DrawPropertyEditor(Node node, PropertyInfo prop, object? value, float editorWidth)
    {
        string propId = $"prop_value_{prop.Name}";
        var lineEditSize = new Vector2(editorWidth, 24); // Default size for editors

        if (prop.PropertyType.IsEnum && prop.CanWrite)
        {
            string[] enumNames = Enum.GetNames(prop.PropertyType);
            int selectedIndex = Array.IndexOf(enumNames, value?.ToString());
            if (selectedIndex == -1) selectedIndex = 0; // Default to first if not found

            if (UI.Combobox(propId, ref selectedIndex, enumNames, lineEditSize))
            {
                object? newValue = Enum.ToObject(prop.PropertyType, selectedIndex);
                prop.SetValue(node, newValue);
            }
        }
        else
        {
            switch (value)
            {
                case bool b when prop.CanWrite:
                    bool isChecked = b;
                    if (UI.Checkbox(propId, "", ref isChecked, size: new Vector2(0, 24)))
                    {
                        prop.SetValue(node, isChecked);
                    }
                    break;

                case Vector2 v:
                    if (prop.CanWrite)
                    {
                        DrawVector2Editor(node, prop, v, propId, editorWidth);
                    }
                    else
                    {
                        UI.Button($"{propId}_display", $"X:{v.X:F2}, Y:{v.Y:F2}", size: new(editorWidth, 24), disabled: true);
                    }
                    break;

                default:
                    string defaultString = value?.ToString() ?? "null";
                    UI.Button($"{propId}_display", defaultString, size: new(editorWidth, 24), disabled: true);
                    break;
            }
        }
    }

    private static void DrawVector2Editor(Node node, PropertyInfo prop, Vector2 value, string baseId, float editorWidth)
    {
        string xId = $"{baseId}_X";
        string yId = $"{baseId}_Y";
        int xIntId = xId.GetHashCode();
        int yIntId = yId.GetHashCode();

        int stateId = HashCode.Combine(node.GetHashCode(), prop.Name.GetHashCode());
        var editState = UI.State.GetOrCreateElement<Vector2EditState>(stateId);

        bool isXFocused = UI.State.FocusedElementId == xIntId;
        bool isYFocused = UI.State.FocusedElementId == yIntId;

        if (!isXFocused)
        {
            if (!float.TryParse(editState.X, out var parsedX) || float.Abs(parsedX - value.X) > 1e-4f)
            {
                editState.X = value.X.ToString("F3");
            }
        }
        if (!isYFocused)
        {
            if (!float.TryParse(editState.Y, out var parsedY) || float.Abs(parsedY - value.Y) > 1e-4f)
            {
                editState.Y = value.Y.ToString("F3");
            }
        }

        UI.BeginHBoxContainer(baseId + "_hbox", UI.Context.Layout.GetCurrentPosition(), 5);
        {
            float lineEditWidth = Math.Max(0, (editorWidth - 5) / 2);
            var lineEditSize = new Vector2(lineEditWidth, 24);

            string localX = editState.X;

            if (UI.InputText(xId, ref localX, lineEditSize))
            {
                editState.X = localX;
                if (float.TryParse(editState.X, out var newX))
                {
                    prop.SetValue(node, new Vector2(newX, value.Y));
                }
            }

            var currentValue = (Vector2)prop.GetValue(node)!;

            string localY = editState.Y;

            if (UI.InputText(yId, ref localY, lineEditSize))
            {
                editState.Y = localY;

                if (float.TryParse(editState.Y, out var newY))
                {
                    prop.SetValue(node, new Vector2(currentValue.X, newY));
                }
            }
        }

        UI.EndHBoxContainer();
    }

    private static string SplitPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        StringBuilder result = new();

        result.Append(input[0]);

        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
            {
                result.Append(' ');
            }

            result.Append(input[i]);
        }

        return result.ToString();
    }
}