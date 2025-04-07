namespace Paramore.Brighter
{
    public interface IRelationalDatabaseInboxQueries
    {
        string AddCommand { get; }
        string ExistsCommand { get; }
        string GetCommand { get; }
    }
}
