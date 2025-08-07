namespace Paramore.Brighter.Inbox.Spanner;

/// <summary>
/// A builder class responsible for generating the Data Definition Language (DDL)
/// for creating an inbox table in Google Cloud Spanner. This table is designed
/// to support the Brighter framework's inbox pattern, ensuring message idempotency.
/// </summary>
/// <remarks>
/// The generated DDL is specifically for a Spanner database using the
/// <a href="https://cloud.google.com/spanner/docs/reference/standard-sql/data-types">GoogleSQL dialect</a>.
/// It defines a table with columns necessary for tracking command messages,
/// including a composite primary key for unique identification and a JSON column
/// for the command body.
/// </remarks>
public class SpannerInboxBuilder
{
    private const string InboxDDL =
        """
        CREATE TABLE IF NOT EXISTS `{0}`(
            `CommandId` STRING(256) NOT NULL,
            `CommandType` STRING(256),
            `CommandBody` JSON,
            `Timestamp` TIMESTAMP,
            `ContextKey` STRING(256) 
        ) PRIMARY KEY (`CommandId`, `ContextKey`)
        """;
    
    /// <summary>
    /// Generates the full DDL statement for creating a Spanner inbox table with a specified name.
    /// </summary>
    /// <param name="inboxTableName">The desired name for the inbox table. This name will be
    /// inserted into the DDL template.</param>
    /// <returns>A string containing the complete `CREATE TABLE IF NOT EXISTS` DDL statement
    /// for the Spanner inbox table, ready for execution.</returns>
    public static string GetDDL(string inboxTableName)
    {
        return string.Format(InboxDDL, inboxTableName);
    }
}
