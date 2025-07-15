using System.Reflection;

namespace Cherris;

public static class NodePropertySetter
{
    private static readonly string[] SpecialProperties = { "type", "name", "path", "children", "Node" };

    public static void SetProperties(Node node, Dictionary<string, object> element, List<(Node, string, object)>? deferredNodeAssignments = null)
    {
        foreach ((string key, object value) in element)
        {
            if (SpecialProperties.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            SetNestedMember(node, key, value, deferredNodeAssignments);
        }
    }

    public static void SetNestedMember(object rootInstance, string fullMemberPath, object value, List<(Node, string, object)>? deferredNodeAssignments = null)
    {
        string[] pathParts = fullMemberPath.Split('/');
        object currentObject = rootInstance;
        for (var i = 0; i < pathParts.Length; i++)
        {
            var memberName = pathParts[i];
            var memberInfo = ReflectionUtils.GetMemberInfo(currentObject.GetType(), memberName);
            bool isFinalSegment = i == pathParts.Length - 1;

            if (isFinalSegment)
            {

                Type memberType = ReflectionUtils.GetMemberType(memberInfo);
                if (value is Dictionary<object, object> dictValue && IsComplexObjectType(memberType))
                {
                    object? existingMemberInstance = ReflectionUtils.GetMemberValue(currentObject, memberInfo);
                    if (existingMemberInstance == null)
                    {
                        existingMemberInstance = Activator.CreateInstance(memberType) ?? throw new InvalidOperationException($"Failed to create instance of {memberType.Name}");
                        ReflectionUtils.SetMemberValue(currentObject, memberInfo, existingMemberInstance);
                    }
                    foreach (KeyValuePair<object, object> entry in dictValue)
                    {
                        string subKey = entry.Key.ToString()!;
                        object subValue = entry.Value;
                        string subPropertyFullPath = fullMemberPath + "/" + subKey;
                        SetNestedMember(rootInstance, subPropertyFullPath, subValue, deferredNodeAssignments);
                    }
                }
                else if (ShouldDeferAssignment(memberType, value))
                {
                    if (rootInstance is Node nodeForDeferral)
                    {
                        deferredNodeAssignments?.Add((nodeForDeferral, fullMemberPath, value));
                    }
                    else
                    {
                        if (deferredNodeAssignments != null)                        {
                            Log.Warning($"Cannot defer assignment for non-Node root target: {rootInstance.GetType().Name} for path {fullMemberPath}. This may be normal if loading non-Node configurations.");
                        }
                        var convertedNonDeferredValue = ValueConversionUtils.ConvertValue(memberType, value);
                        ReflectionUtils.SetMemberValue(currentObject, memberInfo, convertedNonDeferredValue);
                    }
                }
                else
                {
                    var convertedValue = ValueConversionUtils.ConvertValue(memberType, value);
                    ReflectionUtils.SetMemberValue(currentObject, memberInfo, convertedValue);
                }
                return;            }
            else            {
                object? nextObject = ReflectionUtils.GetMemberValue(currentObject, memberInfo);
                if (nextObject == null)
                {
                    nextObject = ReflectionUtils.CreateMemberInstance(memberInfo);
                    ReflectionUtils.SetMemberValue(currentObject, memberInfo, nextObject);
                }
                currentObject = nextObject;
            }
        }
    }

    private static bool ShouldDeferAssignment(Type memberType, object value)
    {
        return memberType.IsSubclassOf(typeof(Node)) && value is string;
    }

    private static bool IsComplexObjectType(Type type)
    {
        if (type.IsEnum || type == typeof(string) || type.IsPrimitive || type == typeof(decimal))
            return false;
        if (type.IsSubclassOf(typeof(Node)))
            return false;
        if (type == typeof(AudioStream) || type == typeof(Sound) || type == typeof(Animation) || type == typeof(Texture) || type == typeof(Font))
            return false;
        if (type == typeof(Vector2) || type == typeof(Color)) return false;        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return false;        if (type.IsClass) return true;
        if (type.IsValueType && !type.IsEnum && !type.IsPrimitive && type != typeof(decimal) && type != typeof(Vector2) && type != typeof(Color))
            return true;

        return false;    }
}