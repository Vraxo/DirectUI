namespace Bankan;

public class KanbanColumn
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<Task> Tasks { get; set; } = [];
}
