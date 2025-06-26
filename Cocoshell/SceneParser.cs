using System;
using System.IO;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace DirectUI
{
    public static class SceneParser
    {
        public static TreeNode<string> Parse(string filePath)
        {
            // NamingConvention is not needed when deserializing to a dictionary.
            var deserializer = new DeserializerBuilder().Build();

            // Deserialize to a dictionary to manually process keys
            var yamlData = deserializer.Deserialize<Dictionary<object, object>>(File.ReadAllText(filePath));

            return ConvertToTreeNode(ConvertDictionary(yamlData));
        }

        private static Dictionary<string, object> ConvertDictionary(Dictionary<object, object> dict)
        {
            var newDict = new Dictionary<string, object>();
            foreach (var kvp in dict)
            {
                if (kvp.Key is string key)
                {
                    newDict[key] = kvp.Value;
                }
            }
            return newDict;
        }

        private static TreeNode<string> ConvertToTreeNode(Dictionary<string, object> nodeData)
        {
            // Use TryGetValue with a case-insensitive check for the 'Node' key.
            if (!nodeData.TryGetValue("Node", out var nodeDescriptorObj) && !nodeData.TryGetValue("node", out nodeDescriptorObj))
            {
                throw new InvalidDataException("YAML node data is missing the 'Node' or 'node' key.");
            }

            string nodeDescriptor = nodeDescriptorObj.ToString(); // e.g., "Player::Player"
            string[] parts = nodeDescriptor.Split(new[] { "::" }, StringSplitOptions.None);
            string nodeName = parts.Length > 1 ? parts[1] : parts[0];
            string nodeType = parts[0];

            string userData = $"Type: {nodeType}"; // For display in the tree

            // Default to not expanded, but expand if there are children.
            var treeNode = new TreeNode<string>(nodeName, userData, false);

            // Use a case-insensitive check for the 'children' key.
            if (nodeData.TryGetValue("children", out var childrenObj) && childrenObj is List<object> childrenList)
            {
                if (childrenList.Count > 0)
                {
                    treeNode.IsExpanded = true; // Expand if it has children
                }

                foreach (var childObj in childrenList)
                {
                    if (childObj is Dictionary<object, object> childDict)
                    {
                        var stringKeyDict = ConvertDictionary(childDict);
                        var childTreeNode = ConvertToTreeNode(stringKeyDict);
                        treeNode.Children.Add(childTreeNode);
                    }
                }
            }

            return treeNode;
        }
    }
}