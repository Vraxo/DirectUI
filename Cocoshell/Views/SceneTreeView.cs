using Cherris;

namespace DirectUI;

public class SceneTreeView
{
    private readonly TreeNode<Node> _uiTreeRoot;
    private readonly TreeStyle _treeStyle = new();

    public Node? SelectedNode { get; private set; }

    public SceneTreeView()
    {
        try
        {
            string scenePath = @"D:\Parsa Stuff\Visual Studio\Cosmocrush\Cosmocrush\Res\Scenes\Menu\Menu.yaml";

            if (File.Exists(scenePath))
            {
                // Use the Cherris engine's PackedScene loader
                var packedScene = new PackedScene(scenePath);
                Node sceneRoot = packedScene.Instantiate<Node>(); // Instantiate the actual scene graph
                _uiTreeRoot = ConvertToUITree(sceneRoot); // Create a UI representation
            }
            else
            {
                _uiTreeRoot = CreateDefaultTree("Scene file not found", scenePath);
            }
        }
        catch (Exception ex)
        {
            _uiTreeRoot = CreateDefaultTree($"Error parsing scene: {ex.Message}", "");
        }
        SelectedNode = null;
    }

    private TreeNode<Node> ConvertToUITree(Node root)
    {
        // The UI TreeNode stores the actual Cherris.Node in its UserData property.
        var uiRoot = new TreeNode<Node>(root.Name, root, root.Children.Any());
        foreach (var child in root.Children)
        {
            uiRoot.AddChild(ConvertToUITree(child));
        }
        return uiRoot;
    }

    public void Draw()
    {
        UI.BeginVBoxContainer("tree_vbox", UI.Context.Layout.GetCurrentPosition(), 0);
        UI.Tree("file_tree", _uiTreeRoot, out var clickedNode, _treeStyle);
        if (clickedNode is not null)
        {
            // When a node is clicked in the UI, we get the real Cherris.Node from its UserData.
            SelectedNode = clickedNode.UserData;
        }
        UI.EndVBoxContainer();
    }

    private static TreeNode<Node> CreateDefaultTree(string reason, string path)
    {
        var errorNode = new Node { Name = reason };
        var root = new TreeNode<Node>("Error", errorNode, true);

        var reasonNode = new Node { Name = reason };
        root.AddChild(reason, reasonNode);

        if (!string.IsNullOrEmpty(path))
        {
            var pathNode = new Node { Name = $"Path: {path}" };
            root.AddChild($"Path: {path}", pathNode);
        }

        return root;
    }
}