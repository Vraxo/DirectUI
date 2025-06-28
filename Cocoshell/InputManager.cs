using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cocoshell.Input;

public static class InputMapManager
{
    public static Dictionary<string, List<InputBinding>> Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Warning: Input map file not found at '{filePath}'. Returning empty map.");
            return [];
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            string text = File.ReadAllText(filePath);
            var loadedMap = deserializer.Deserialize<Dictionary<string, List<InputBinding>>>(text);
            
            return loadedMap ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing input map file '{filePath}': {ex.Message}. Returning empty map.");
            return [];
        }
    }

    public static void Save(string filePath, Dictionary<string, List<InputBinding>> map)
    {
        try
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .DisableAliases()
                .Build();
            
            string yaml = serializer.Serialize(map);
            
            File.WriteAllText(filePath, yaml);
            Console.WriteLine($"Successfully saved input map to '{filePath}'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving input map file '{filePath}': {ex.Message}");
        }
    }
}