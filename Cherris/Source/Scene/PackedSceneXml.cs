using System.Xml.Linq;
using System.Reflection;
using System.Globalization;

namespace Cherris;

public sealed class PackedSceneXml(string path)
{
    private readonly string _path = path;

    public T Instantiate<T>() where T : Node
    {
        var doc = XDocument.Load(_path);
        var rootElement = doc.Root;

        if (rootElement == null || rootElement.Name != "Node")
            throw new Exception($"Invalid root element in scene file '{_path}'");

        var rootNode = (T)LoadNode(rootElement);
        return rootNode;
    }

    private Node LoadNode(XElement element)
    {
        string nodeName = element.Attribute("name")?.Value
            ?? throw new Exception($"Missing 'name' attribute on element <{element.Name}>");
        string nodeType = element.Name.LocalName;

        var node = CreateNodeInstance(nodeType);
        node.Name = nodeName;
        foreach (var childElement in element.Elements())
        {
            if (IsPropertyElement(childElement.Name.LocalName))
            {
                SetNodeProperty(node, childElement);
            }
            else
            {
                var childNode = LoadNode(childElement);
                node.AddChild(childNode, childNode.Name);
            }
        }

        return node;
    }

    private static bool IsPropertyElement(string elementName)
    {
        return elementName == "Position" ||
               elementName == "Scale" ||
               elementName == "Size" ||
               elementName == "FilePath" ||
               elementName == "AutoPlay" ||
               elementName == "Text";
    }

    private void SetNodeProperty(Node node, XElement element)
    {
        string propertyName = element.Name.LocalName;
        var propertyInfo = node.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        if (propertyInfo == null)
            return;
        if (propertyInfo.PropertyType == typeof(Vector2))
        {
            float x = float.Parse(element.Attribute("x")?.Value ?? "0", CultureInfo.InvariantCulture);
            float y = float.Parse(element.Attribute("y")?.Value ?? "0", CultureInfo.InvariantCulture);
            propertyInfo.SetValue(node, new Vector2(x, y));
        }
        else if (propertyInfo.PropertyType == typeof(bool))
        {
            propertyInfo.SetValue(node, bool.Parse(element.Value));
        }
        else if (propertyInfo.PropertyType == typeof(string))
        {
            propertyInfo.SetValue(node, element.Value);
        }
        else
        {
            object converted = Convert.ChangeType(element.Value, propertyInfo.PropertyType, CultureInfo.InvariantCulture);
            propertyInfo.SetValue(node, converted);
        }
    }

    private static Node CreateNodeInstance(string typeName)
    {
        if (!typeName.Contains('.'))
        {
            typeName = "Cherris." + typeName;
        }

        var type = Type.GetType(typeName)
                   ?? throw new Exception($"Unknown node type: '{typeName}'");

        return Activator.CreateInstance(type) as Node
               ?? throw new Exception($"Could not create instance of type '{typeName}'");
    }
}
