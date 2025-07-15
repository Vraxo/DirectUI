using Cherris;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class FileLoader
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .Build();

    public static T Load<T>(string filePath) where T : new()
    {
        string yamlContent = File.ReadAllText(filePath);
        object data = _deserializer.Deserialize<object>(yamlContent);

        T instance = new();
        List<(Node, string, object)>? deferredAssignments = null;
        if (instance is Node)
        {
            deferredAssignments = new List<(Node, string, object)>();
        }

        ProcessYamlData(instance, data, "", deferredAssignments);
        return instance;
    }

    private static void ProcessYamlData(object target, object yamlData, string currentPath, List<(Node, string, object)>? deferredNodeAssignments)
    {
        switch (yamlData)
        {
            case Dictionary<object, object> dict:
                foreach (KeyValuePair<object, object> entry in dict)
                {
                    string key = entry.Key.ToString()!;
                    string newPath = string.IsNullOrEmpty(currentPath)
                        ? key
                        : $"{currentPath}/{key}";

                    ProcessYamlData(target, entry.Value, newPath, deferredNodeAssignments);
                }
                break;

            case List<object> list:
                NodePropertySetter.SetNestedMember(target, currentPath, list, deferredNodeAssignments);
                break;

            default:                NodePropertySetter.SetNestedMember(target, currentPath, yamlData, deferredNodeAssignments);
                break;
        }
    }
}