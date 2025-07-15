namespace Cherris;

public static class NodeEmoji
{
    public static string GetEmojiForNodeType(Node node)
    {
        return node switch
        {
            _ => "⭕",
        };
    }
}
