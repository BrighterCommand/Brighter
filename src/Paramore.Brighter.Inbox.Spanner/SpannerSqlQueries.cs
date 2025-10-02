namespace Paramore.Brighter.Inbox.Spanner;

/// <summary>
/// The spanner SQL queries
/// </summary>
public class SpannerSqlQueries : IRelationalDatabaseInboxQueries
{
    /// <inheritdoc />
    public string AddCommand => "INSERT INTO `{0}` (`CommandID`, `CommandType`, `CommandBody`, `Timestamp`, `ContextKey`) VALUES (@CommandID, @CommandType, @CommandBody, @Timestamp, @ContextKey)";

    /// <inheritdoc />
    public string ExistsCommand => "SELECT `CommandID` FROM `{0}` WHERE `CommandID` = @CommandID AND `ContextKey` = @ContextKey LIMIT 1";

    /// <inheritdoc />
    public string GetCommand => "SELECT * FROM `{0}` where `CommandID` = @CommandID AND `ContextKey` = @ContextKey";
}
