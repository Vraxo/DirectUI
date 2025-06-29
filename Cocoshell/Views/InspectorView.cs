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

        string nodeName = selectedNode.Text;

        if (UI.LineEdit("node_name_edit", ref nodeName, new(availableWidth, 24)))
        {
            selectedNode.Text = nodeName;
        }
    }
}