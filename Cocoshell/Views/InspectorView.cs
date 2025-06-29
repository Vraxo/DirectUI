using System.Numerics;
using Vortice.DirectWrite;

namespace DirectUI;

public class InspectorView
{
    private const float PanelPadding = 10f;

    private ButtonStyle titleStyle = new()
    {
        FontWeight = FontWeight.SemiBold,
        FontSize = 16f
    };

    public void Draw(TreeNode<string>? selectedNode, float panelWidth)
    {
        float availableWidth = panelWidth - (PanelPadding * 2);

        UI.Label(
            "inspector_title",
            "Inspector",
            size: new(availableWidth, 0),
            style: titleStyle,
            textAlignment: new(HAlignment.Center, VAlignment.Center)
        );

        if (selectedNode is null)
        {
            UI.Button("no_selection_label", "No node selected.", disabled: true, autoWidth: true);
            return;
        }

        // A VBox to layout property rows vertically.
        UI.BeginVBoxContainer("inspector_properties_vbox", UI.Context.Layout.GetCurrentPosition(), 5f);
        {
            // --- Property Row for "Name" ---
            // Use a 2-column grid for the label and the value editor.
            float gridGap = 5f;
            // GridContainer creates uniform columns. We calculate the width for each cell.
            float cellWidth = (availableWidth - gridGap) / 2f;

            UI.BeginGridContainer(
                id: $"grid_name_{selectedNode.GetHashCode()}",
                position: UI.Context.Layout.GetCurrentPosition(),
                availableSize: new Vector2(availableWidth, 24), // Height for one row.
                numColumns: 2,
                gap: new Vector2(gridGap, 0)
            );
            {
                // Column 1: Label
                UI.Label(
                    "name_label",
                    "Name",
                    size: new(cellWidth, 24), // Explicitly size the label to fit the cell.
                    textAlignment: new(HAlignment.Left, VAlignment.Center)
                );

                // Column 2: LineEdit
                string nodeName = selectedNode.Text;
                // The LineEdit must also be sized to fit its cell.
                if (UI.LineEdit($"node_name_edit_{selectedNode.GetHashCode()}", ref nodeName, new(cellWidth, 24)))
                {
                    selectedNode.Text = nodeName;
                }
            }
            UI.EndGridContainer();

            // Future properties can be added here as more grid containers...
        }
        UI.EndVBoxContainer();
    }
}