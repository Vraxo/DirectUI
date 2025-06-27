using YamlDotNet.Serialization;

namespace DirectUI;

public static class SceneParser
{
    public static TreeNode<string> Parse(string filePath)
    {
        var deserializer = new DeserializerBuilder().Build();
        var yamlData = deserializer.Deserialize<Dictionary<object, object>>(File.ReadAllText(filePath));

        return ConvertToTreeNode(ConvertDictionary(yamlData));
    }

    private static Dictionary<string, object> ConvertDictionary(Dictionary<object, object> dict)
    {
        Dictionary<string, object> newDict = [];

        foreach (KeyValuePair<object, object> kvp in dict)
        {
            if (kvp.Key is not string key)
            {
                continue;
            }

            newDict[key] = kvp.Value;
        }

        return newDict;
    }

    private static TreeNode<string> ConvertToTreeNode(Dictionary<string, object> nodeData)
    {
        if (!nodeData.TryGetValue("Node", out var nodeDescriptorObj) && !nodeData.TryGetValue("node", out nodeDescriptorObj))
        {
            throw new InvalidDataException("YAML node data is missing the 'Node' or 'node' key.");
        }

        string nodeDescriptor = nodeDescriptorObj.ToString();
        string[] parts = nodeDescriptor.Split(["::"], StringSplitOptions.None);
        string nodeName = parts.Length > 1 ? parts[1] : parts[0];
        string nodeType = parts[0];

        string userData = $"Type: {nodeType}";

        TreeNode<string> treeNode = new(nodeName, userData, false);

        if (!nodeData.TryGetValue("children", out var childrenObj) || childrenObj is not List<object> childrenList)
        {
            return treeNode;
        }

        if (childrenList.Count > 0)
        {
            treeNode.IsExpanded = true;
        }

        foreach (object childObj in childrenList)
        {
            if (childObj is not Dictionary<object, object> childDict)
            {
                continue;
            }

            Dictionary<string, object> stringKeyDict = ConvertDictionary(childDict);
            TreeNode<string> childTreeNode = ConvertToTreeNode(stringKeyDict);
            treeNode.Children.Add(childTreeNode);
        }

        return treeNode;
    }
}