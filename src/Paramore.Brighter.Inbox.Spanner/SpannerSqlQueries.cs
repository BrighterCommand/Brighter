namespace Paramore.Brighter.Inbox.Spanner;

/// <summary>
/// The spanner SQL queries
/// </summary>
public class SpannerSqlQueries : IRelationalDatabaseInboxQueries, IRelationalDatabaseInboxCausationQueries
{
    /// <inheritdoc />
    public string AddCommand => "INSERT INTO `{0}` (`CommandID`, `CommandType`, `CommandBody`, `Timestamp`, `ContextKey`) VALUES (@CommandID, @CommandType, @CommandBody, @Timestamp, @ContextKey)";

    /// <inheritdoc />
    public string ExistsCommand => "SELECT `CommandID` FROM `{0}` WHERE `CommandID` = @CommandID AND `ContextKey` = @ContextKey LIMIT 1";

    /// <inheritdoc />
    public string GetCommand => "SELECT * FROM `{0}` where `CommandID` = @CommandID AND `ContextKey` = @ContextKey";

    /// <inheritdoc />
    public string AddCausationCommand => "INSERT INTO `{0}` (`CommandID`, `CommandType`, `CommandBody`, `Timestamp`, `ContextKey`, `CausationId`) VALUES (@CommandID, @CommandType, @CommandBody, @Timestamp, @ContextKey, @CausationId)";

    /// <inheritdoc />
    public string GetCausationIdCommand => "SELECT `CausationId` FROM `{0}` WHERE `CommandID` = @CommandID AND `ContextKey` = @ContextKey LIMIT 1";

    /// <inheritdoc />
    public string CausationColumnExistsCommand => "SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{0}' AND COLUMN_NAME = 'CausationId'";
}
