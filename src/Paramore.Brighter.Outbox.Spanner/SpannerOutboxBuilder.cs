namespace Paramore.Brighter.Outbox.Spanner;

public class SpannerOutboxBuilder
{
    private const string TextOutboxDdl =
        """
        CREATE TABLE IF NOT EXISTS `{0}`
        (
          `Id` STRING(36) NOT NULL,
          `MessageId` STRING(255) NOT NULL, 
          `Topic` STRING(255) NULL,
          `MessageType` STRING(32) NULL, 
          `Timestamp` TIMESTAMP NULL,
          `CorrelationId` STRING(255) NULL,
          `ReplyTo` STRING(255) NULL,
          `ContentType` STRING(128) NULL,
          `PartitionKey` STRING(128) NULL,
          `Dispatched` TIMESTAMP NULL,
          `HeaderBag` STRING(MAX) NULL,
          `Body` STRING(MAX) NULL,
          `Source` STRING(255) NULL,
          `Type` STRING(255) NULL,
          `DataSchema` STRING(255) NULL,
          `Subject` STRING(255) NULL,
          `TraceParent` STRING(255) NULL,
          `TraceState` STRING(255) NULL,
          `Baggage` STRING(MAX) NULL,
           PRIMARY KEY (`Id`)
        );
        """;

    private const string BinaryOutboxDdl = 
        """
        CREATE TABLE IF NOT EXISTS `{0}`
        (
          `Id` STRING(36) NOT NULL,
          `MessageId` STRING(255) NOT NULL, 
          `Topic` STRING(255) NULL,
          `MessageType` STRING(32) NULL, 
          `Timestamp` TIMESTAMP NULL,
          `CorrelationId` STRING(255) NULL,
          `ReplyTo` STRING(255) NULL,
          `ContentType` STRING(128) NULL,
          `PartitionKey` STRING(128) NULL,
          `Dispatched` TIMESTAMP NULL,
          `HeaderBag` STRING(MAX) NULL,
          `Body` BYTES(MAX) NULL,
          `Source` STRING(255) NULL,
          `Type` STRING(255) NULL,
          `DataSchema` STRING(255) NULL,
          `Subject` STRING(255) NULL,
          `TraceParent` STRING(255) NULL,
          `TraceState` STRING(255) NULL,
          `Baggage` STRING(MAX) NULL,
           PRIMARY KEY (`Id`)
        );
        """;
        
    /// <summary>
   /// Get the DDL required to create the Outbox in Postgres
   /// </summary>
   /// <param name="outboxTableName">The name you want to use for the table</param>
   /// <param name="binaryMessagePayload"></param>
   /// <returns>The required DDL</returns>
   public static string GetDDL(string outboxTableName, bool binaryMessagePayload = false)
   {
       return binaryMessagePayload ? string.Format(BinaryOutboxDdl, outboxTableName) : string.Format(TextOutboxDdl, outboxTableName);
   }
}
