namespace Paramore.Brighter.Outbox.Spanner;

public class SpannerQueries : IRelationDatabaseOutboxQueries
{
    /// <inheritdoc />
    public string PagedDispatchedCommand => "SELECT * FROM `{0}` WHERE `Dispatched` IS NOT NULL AND `Dispatched` < @DispatchedSince ORDER BY `Timestamp` DESC LIMIT @Take OFFSET @Skip";

    /// <inheritdoc />
    public string PagedReadCommand => "SELECT * FROM `{0}` ORDER BY `Timestamp` ASC LIMIT @Take OFFSET @Skip";
    
    /// <inheritdoc />
    public string PagedOutstandingCommand => "SELECT * FROM `{0}` WHERE `Dispatched` IS NULL AND `Timestamp` < @TimestampSince ORDER BY `Timestamp` DESC LIMIT @Take OFFSET @Skip";

    /// <inheritdoc />
    public string PagedOutstandingCommandInStatement => "AND `Topic` NOT IN ( {0} )";

    /// <inheritdoc />
    public string AddCommand => 
        "INSERT INTO `{0}` " +
        "(`MessageId`,`MessageType`,`Topic`,`Timestamp`,`CorrelationId`,`ReplyTo`,`ContentType`,`PartitionKey`,`HeaderBag`,`Body`" +
        ",`Source`,`Type`,`DataSchema`,`Subject`,`TraceParent`,`TraceState`,`Baggage`) " +
        "VALUES (@MessageId,@MessageType,@Topic,@Timestamp,@CorrelationId,@ReplyTo,@ContentType,@PartitionKey,@HeaderBag,@Body,@Source,@Type,@DataSchema,@Subject,@TraceParent,@TraceState,@Baggage)";
    
    /// <inheritdoc />
    public string BulkAddCommand => 
        "INSERT INTO `{0}` " +
        "(`MessageId`,`MessageType`,`Topic`,`Timestamp`,`CorrelationId`,`ReplyTo`,`ContentType`,`PartitionKey`,`HeaderBag`,`Body`" +
        ",`Source`,`Type`,`DataSchema`,`Subject`,`TraceParent`,`TraceState`,`Baggage`) " +
        "VALUES {1}";
    
    /// <inheritdoc />
    public string MarkDispatchedCommand => "UPDATE `{0}` SET `Dispatched` = @DispatchedAt WHERE `MessageId` = @MessageId";

    /// <inheritdoc />
    public string MarkMultipleDispatchedCommand => "UPDATE `{0}` SET `Dispatched` = @DispatchedAt WHERE `MessageId` IN ( {1} )";

    /// <inheritdoc />
    public string GetMessageCommand => "SELECT * FROM `{0}` WHERE `MessageId` = @MessageId"; 

    /// <inheritdoc />
    public string GetMessagesCommand => "SELECT * FROM `{0}` WHERE `MessageId` IN ( {1} ) ORDER BY `Timestamp` ASC";
    
    /// <inheritdoc />
    public string DeleteMessagesCommand => "DELETE FROM `{0}` WHERE `MessageId` IN ( {1} )";

    /// <inheritdoc />
    public string GetNumberOfOutstandingMessagesCommand => "SELECT COUNT(1) FROM `{0}` WHERE `Dispatched` IS NULL";
}
