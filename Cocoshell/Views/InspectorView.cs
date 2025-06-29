using Vortice.DirectWrite;

namespace DirectUI;

public class InspectorView
{
    private const float PanelPadding = 10f;

    public static void Draw(TreeNode<string>? selectedNode, float panelWidth)
    {
        ButtonStyle titleStyle = new()
        {
            FontWeight = FontWeight.SemiBold,
            FontSize = 16f
        };

        UI.Label("inspector_title", "Inspector", style: titleStyle);

        UI.Button("inspector_separator", "", disabled: true, size: new(0, 10));

        if (selectedNode is null)
        {
            return;
        }

        string nodeName = selectedNode.Text;
        float availableWidth = panelWidth - (PanelPadding * 2);

        if (UI.LineEdit("node_name_edit", ref nodeName, new(availableWidth, 24)))
        {
            selectedNode.Text = nodeName;
        }

        else
        {
            UI.Button("no_selection_label", "No node selected.", disabled: true, autoWidth: true);
        }
    }
}