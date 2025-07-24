namespace Paramore.Brighter
{
    public interface IRelationDatabaseOutboxQueries
    {
        string PagedDispatchedCommand { get; }
        string PagedReadCommand { get; }
        string PagedOutstandingCommand { get; }
        string PagedOutstandingCommandInStatement { get; }
        string AddCommand { get; }
        string BulkAddCommand { get; }
        string MarkDispatchedCommand { get; }
        string MarkMultipleDispatchedCommand { get; }
        string GetMessageCommand { get; }
        string GetMessagesCommand { get; }
        string DeleteMessagesCommand { get; }
        string GetNumberOfOutstandingMessagesCommand { get; }
    }
}
