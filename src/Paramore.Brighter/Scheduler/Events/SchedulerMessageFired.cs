namespace Paramore.Brighter.Scheduler.Events;

// TODO Add doc

public class SchedulerMessageFired(string id) : Event(id)
{
    public SchedulerFireType FireType { get; set; } = SchedulerFireType.Send;
    public string MessageType { get; set; } = string.Empty;
    public string MessageData { get; set; } = string.Empty;
}

public enum SchedulerFireType
{
    Send,
    Publish,
    Post
}
