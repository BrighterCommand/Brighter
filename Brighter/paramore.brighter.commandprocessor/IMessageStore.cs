namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Base interface for both sync and async
    /// message store interfaces
    /// </summary>
    public interface IMessageStore<in T> where T : Message
    { }
}
