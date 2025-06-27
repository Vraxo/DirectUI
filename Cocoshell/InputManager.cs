using System;
using System.Collections.Generic;
using System.IO;
using DirectUI; // For TreeNode (not used here but good practice), and other potential shared types
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cocoshell.Input
{
    public static class InputMapManager
    {
        /// <summary>
        /// Loads an input map from a YAML file.
        /// Note: This assumes a valid YAML structure where each action maps to a LIST of bindings.
        /// It will fail to parse non-standard formats but will not crash the application.
        /// </summary>
        public static Dictionary<string, List<InputBinding>> Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Warning: Input map file not found at '{filePath}'. Returning empty map.");
                return new Dictionary<string, List<InputBinding>>();
            }

            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .Build();

                var text = File.ReadAllText(filePath);
                var loadedMap = deserializer.Deserialize<Dictionary<string, List<InputBinding>>>(text);
                return loadedMap ?? new Dictionary<string, List<InputBinding>>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing input map file '{filePath}': {ex.Message}. Returning empty map.");
                return new Dictionary<string, List<InputBinding>>();
            }
        }

        /// <summary>
        /// Saves the given input map to a YAML file.
        /// </summary>
        public static void Save(string filePath, Dictionary<string, List<InputBinding>> map)
        {
            try
            {
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .DisableAliases() // Prefer explicit structure over YAML aliases
                    .Build();
                var yaml = serializer.Serialize(map);
                File.WriteAllText(filePath, yaml);
                Console.WriteLine($"Successfully saved input map to '{filePath}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving input map file '{filePath}': {ex.Message}");
            }
        }
    }
}