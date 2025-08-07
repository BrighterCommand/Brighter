namespace Paramore.Brighter.Outbox.Spanner;

public class SpannerOutboxBuilder
{
    private const string TextOutboxDdl =
        """
        CREATE TABLE IF NOT EXISTS `{0}`
        (
          `MessageId` STRING(255) NOT NULL, 
          `Topic` STRING(255),
          `MessageType` STRING(32), 
          `Timestamp` TIMESTAMP,
          `CorrelationId` STRING(255),
          `ReplyTo` STRING(255),
          `ContentType` STRING(128),
          `PartitionKey` STRING(128),
          `Dispatched` TIMESTAMP,
          `HeaderBag` STRING(MAX),
          `Body` STRING(MAX),
          `Source` STRING(255),
          `Type` STRING(255),
          `DataSchema` STRING(255),
          `Subject` STRING(255),
          `TraceParent` STRING(255),
          `TraceState` STRING(255),
          `Baggage` STRING(MAX)
        ) PRIMARY KEY (`MessageId`)
        """;

    private const string BinaryOutboxDdl = 
        """
        CREATE TABLE IF NOT EXISTS `{0}`
        (
          `MessageId` STRING(255) NOT NULL, 
          `Topic` STRING(255),
          `MessageType` STRING(32), 
          `Timestamp` TIMESTAMP,
          `CorrelationId` STRING(255),
          `ReplyTo` STRING(255),
          `ContentType` STRING(128),
          `PartitionKey` STRING(128),
          `Dispatched` TIMESTAMP,
          `HeaderBag` STRING(MAX),
          `Body` BYTES(MAX),
          `Source` STRING(255),
          `Type` STRING(255),
          `DataSchema` STRING(255),
          `Subject` STRING(255),
          `TraceParent` STRING(255),
          `TraceState` STRING(255),
          `Baggage` STRING(MAX)
        ) PRIMARY KEY (`MessageId`)
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
