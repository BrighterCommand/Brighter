using Paramore.Brighter;

namespace TodoApi;

/// <summary>
/// Event raised when a new Todo item is created
/// </summary>
public class TodoCreated : Event
{
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public TodoCreated() : base(Guid.NewGuid())
    {
    }

    public TodoCreated(string title, bool isCompleted = false) : base(Guid.NewGuid())
    {
        Title = title;
        IsCompleted = isCompleted;
        CreatedAt = DateTime.UtcNow;
    }
}
