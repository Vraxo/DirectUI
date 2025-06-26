namespace DirectUI;

public class SceneNode
{
    public string Name { get; set; } = "Unnamed";
    public string UserData { get; set; } = "";
    public bool IsExpanded { get; set; } = false;
    public List<SceneNode>? Children { get; set; }
}