using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Cherris;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public class InspectorView
{
    private const float PanelGap = 10f; // Copied from MainView for internal layout
    private const float PanelPadding = 10f;
    private const float GridGap = 8f;

    private static readonly HashSet<string> s_ignoredProperties =
    [
        "Parent", "Children", "AbsolutePath", "ScaledSize"
    ];

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

    public void Draw(Node? selectedNode, float panelWidth, float panelHeight)
    {
        float availableContentWidth = panelWidth - (PanelPadding * 2);

        // Use a VBox to manage the layout of the header and the scrollable area.
        UI.BeginVBoxContainer("inspector_outer_vbox", UI.Context.Layout.GetCurrentPosition(), PanelGap);
        {
            // --- Header ---
            DrawHeader(availableContentWidth);

            // --- Scrollable Area ---
            // Calculate height available for the scroll region after the header and gap are accounted for.
            Vector2 headerSize = UI.Resources.MeasureText(UI.Context.DWriteFactory, "Inspector", _titleStyle);
            float scrollableHeight = panelHeight - headerSize.Y - PanelGap;
            if (scrollableHeight < 0) scrollableHeight = 0;

            var scrollableSize = new Vector2(availableContentWidth, scrollableHeight);

            UI.BeginScrollableRegion("inspector_scroll", scrollableSize, out float innerContentWidth);
            {
                // A VBox for the content inside the scroll region
                UI.BeginVBoxContainer("inspector_properties_vbox", UI.Context.Layout.GetCurrentPosition(), 8f);
                {
                    if (selectedNode is null)
                    {
                        UI.Button("no_selection_label", "No node selected.", disabled: true, autoWidth: true);
                    }
                    else
                    {
                        // The innerContentWidth is provided by BeginScrollableRegion,
                        // which accounts for the potential width of the scrollbar.
                        DrawNodeInfo(selectedNode, innerContentWidth);
                        DrawAllProperties(selectedNode, innerContentWidth);
                    }
                }
                UI.EndVBoxContainer();
            }
            UI.EndScrollableRegion();
        }
        UI.EndVBoxContainer();
    }

    private void DrawHeader(float availableWidth)
    {
        UI.Label(
            "inspector_title",
            "Inspector",
            size: new(availableWidth, 0),
            style: _titleStyle,
            textAlignment: new(HAlignment.Center, VAlignment.Center)
        );
    }

    private void DrawNodeInfo(Node selectedNode, float availableWidth)
    {
        UI.Label("type_header", $"Type: {selectedNode.GetType().Name}", style: _propertyLabelStyle);

        Vector2 linePos = UI.Context.Layout.GetCurrentPosition() + new Vector2(0, -4);
        var lineBrush = UI.Resources.GetOrCreateBrush(UI.Context.RenderTarget, DefaultTheme.NormalBorder);
        if (lineBrush != null)
        {
            UI.Context.RenderTarget.DrawLine(
                linePos,
                linePos + new Vector2(availableWidth, 0),
                lineBrush,
                1f);
        }
    }

    private void DrawAllProperties(Node selectedNode, float availableWidth)
    {
        var properties = selectedNode.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !s_ignoredProperties.Contains(p.Name));

        foreach (var prop in properties)
        {
            DrawPropertyRow(selectedNode, prop, availableWidth);
        }
    }

    private void DrawPropertyRow(Node node, PropertyInfo prop, float availableWidth)
    {
        float labelCellWidth = (availableWidth - GridGap) * 0.4f;
        float valueCellWidth = (availableWidth - GridGap) * 0.6f;
        string gridId = $"grid_prop_{node.Name}_{prop.Name}";

        UI.BeginGridContainer(gridId, UI.Context.Layout.GetCurrentPosition(), new(availableWidth, 24), 2, new(GridGap, 0));
        try
        {
            UI.Label(
                $"prop_label_{prop.Name}",
                SplitPascalCase(prop.Name),
                size: new(labelCellWidth, 24),
                textAlignment: new(HAlignment.Left, VAlignment.Center)
            );

            object? value = prop.GetValue(node, null);

            DrawPropertyEditor(node, prop, value, valueCellWidth);
        }
        catch (Exception ex)
        {
            UI.Label($"prop_error_{prop.Name}", $"Error: {ex.Message}");
        }
        UI.EndGridContainer();
    }

    private void DrawPropertyEditor(Node node, PropertyInfo prop, object? value, float editorWidth)
    {
        string propId = $"prop_value_{prop.Name}";

        switch (value)
        {
            case bool b when prop.CanWrite:
                bool isChecked = b;
                if (UI.Checkbox(propId, "", ref isChecked))
                {
                    prop.SetValue(node, isChecked);
                }
                break;

            default:
                string valueString = value switch
                {
                    Vector2 v => $"X:{v.X:F2}, Y:{v.Y:F2}",
                    _ => value?.ToString() ?? "null",
                };

                UI.Button($"{propId}_display", valueString, size: new(editorWidth, 24), disabled: true);
                break;
        }
    }

    private static string SplitPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        System.Text.StringBuilder result = new();

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