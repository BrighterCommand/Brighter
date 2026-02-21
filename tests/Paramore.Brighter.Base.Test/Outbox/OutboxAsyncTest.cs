using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Base.Test.Outbox;

public abstract class OutboxAsyncTest<TTransaction> : IAsyncLifetime 
{
    protected abstract IAmAnOutboxAsync<Message, TTransaction> Outbox { get; }

    protected List<Message> CreatedMessages { get; } = [];
    
    public async Task InitializeAsync()
    {
        await BeforeEachTestAsync();
    }

    public async Task DisposeAsync()
    {
        await AfterEachTestAsync();
    }

    protected virtual async Task BeforeEachTestAsync()
    {
        await CreateStoreAsync();
    }
    
    protected virtual Task CreateStoreAsync()
    {
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        AfterEachTestAsync().GetAwaiter().GetResult();
    }

    protected virtual async Task AfterEachTestAsync()
    {
        await DeleteStoreAsync();
    }

    protected virtual Task DeleteStoreAsync()
    {
        return Task.CompletedTask;
    }


    protected abstract Task<IEnumerable<Message>> GetAllMessagesAsync();
    
    protected abstract IAmABoxTransactionProvider<TTransaction> CreateTransactionProvider();

   protected virtual Message CreateRandomMessage(DateTimeOffset? timestamp = null)
    {
        var random = new Random();
        var messageHeader = new MessageHeader(
            messageId:    Id.Random(),
            topic:        new RoutingKey(Uuid.NewAsString()),
            messageType:  MessageType.MT_DOCUMENT,
            source:       new Uri(Uuid.NewAsString(), UriKind.Relative),
            type:         new CloudEventsType(Uuid.NewAsString()),
            timeStamp:    timestamp ?? DateTimeOffset.UtcNow,
            correlationId:Id.Random(),
            replyTo:      new RoutingKey(Uuid.NewAsString()),
            contentType:  new ContentType(MediaTypeNames.Text.Plain),
            partitionKey: Uuid.NewAsString(),
            dataSchema:   new Uri("https://schema.test"),
            subject:      Uuid.NewAsString(),
            handledCount: random.Next(),
            delayed:      TimeSpan.FromMilliseconds(5),
            traceParent:  "00-abcdef0123456789-abcdef0123456789-01",
            traceState:   "state123",
            baggage:      new Baggage(),
            workflowId: Id.Random(),
            jobId: Id.Random());

        messageHeader.Bag.Add("header1", Uuid.NewAsString());
        messageHeader.Bag.Add("header2", Uuid.NewAsString());
        messageHeader.Bag.Add("header3", Uuid.NewAsString());
        messageHeader.Bag.Add("header4", Uuid.NewAsString());
        messageHeader.Bag.Add("header5", Uuid.NewAsString());
        var message = new Message(messageHeader, new MessageBody(Uuid.NewAsString()));

        CreatedMessages.Add(message);
        return message;
    }

    [Fact]
    public async Task When_Deleting_One_Message_It_Should_Be_Removed_From_Outbox()
    {
        // Arrange
        var context = new RequestContext();
        var firstMessage = CreateRandomMessage();
        var secondMessage = CreateRandomMessage();
        var thirdMessage = CreateRandomMessage();
        
        // Act
        await Outbox.AddAsync(firstMessage, context);
        await Outbox.AddAsync(secondMessage, context);
        await Outbox.AddAsync(thirdMessage, context);
        
        await Outbox.DeleteAsync([firstMessage.Id], context);
        
        // Assert
        var messages = (await Outbox
            .OutstandingMessagesAsync(TimeSpan.Zero, context))
            .ToArray();
        
        Assert.DoesNotContain(firstMessage.Id, messages.Select(x => x.Id));
        Assert.Contains(secondMessage.Id, messages.Select(x => x.Id));
        Assert.Contains(thirdMessage.Id, messages.Select(x => x.Id));
    }
    
    [Fact]
    public async Task When_Deleting_Multiple_Messages_They_Should_Be_Removed_From_Outbox()
    {
        // Arrange
        var context = new RequestContext();
        var firstMessage = CreateRandomMessage();
        var secondMessage = CreateRandomMessage();
        var thirdMessage = CreateRandomMessage();
        
        // Act
        await Outbox.AddAsync(firstMessage, context);
        await Outbox.AddAsync(secondMessage, context);
        await Outbox.AddAsync(thirdMessage, context);
        
        await Outbox.DeleteAsync([firstMessage.Id, secondMessage.Id, thirdMessage.Id], context);
        
        // Assert
        var messages = (await Outbox
            .OutstandingMessagesAsync(TimeSpan.Zero, context))
            .ToArray();
        
        Assert.DoesNotContain(firstMessage.Id, messages.Select(x => x.Header.MessageId));
        Assert.DoesNotContain(secondMessage.Id, messages.Select(x => x.Header.MessageId));
        Assert.DoesNotContain(thirdMessage.Id, messages.Select(x => x.Header.MessageId));
    }

    [Fact]
    public async Task When_Retrieving_All_Messages_They_Should_Include_Dispatched_And_Undispatched()
    {
        // Arrange
        var context = new RequestContext();
        var earliest = CreateRandomMessage();
        var dispatched = CreateRandomMessage();
        var undispatched = CreateRandomMessage();
        
        await Outbox.AddAsync([earliest, dispatched, undispatched], context);
        await Outbox.MarkDispatchedAsync(earliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await Outbox.MarkDispatchedAsync(dispatched.Id, context, DateTime.UtcNow.AddSeconds(-30));

        // Act
        var messages = (await GetAllMessagesAsync()).ToArray();

        // Assert
        Assert.True(messages.Length >= 3, "Expecting at least 3 messages");
        Assert.Contains(earliest.Id, messages.Select(x => x.Id));
        Assert.Contains(dispatched.Id, messages.Select(x => x.Id));
        Assert.Contains(undispatched.Id, messages.Select(x => x.Id));
    }
    
    [Fact]
    public async Task When_Retrieving_Messages_By_Ids_It_Should_Return_Only_Requested_Messages()
    {
        // Arrange
        var context = new RequestContext();
        var earliest = CreateRandomMessage();
        var dispatched = CreateRandomMessage();
        var undispatched = CreateRandomMessage();
        
        await Outbox.AddAsync([earliest, dispatched, undispatched], context);
        await Outbox.MarkDispatchedAsync(earliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await Outbox.MarkDispatchedAsync(dispatched.Id, context, DateTime.UtcNow.AddSeconds(-30));

        // Act
        var messages = (await Outbox
            .GetAsync([earliest.Id, undispatched.Id], context))
            .ToArray();

        // Assert
        Assert.Equal(2, messages.Length);
        Assert.Contains(earliest.Id, messages.Select(x => x.Id));
        Assert.DoesNotContain(dispatched.Id, messages.Select(x => x.Id));
        Assert.Contains(undispatched.Id, messages.Select(x => x.Id));
    }
    
    [Fact]
    public async Task When_Retrieving_A_Message_By_Id_It_Should_Return_The_Correct_Message()
    {
        // Arrange
        var context = new RequestContext();
        var earliest = CreateRandomMessage();
        var dispatched = CreateRandomMessage();
        var undispatched = CreateRandomMessage();
        
        await Outbox.AddAsync([earliest, dispatched, undispatched], context);
        await Outbox.MarkDispatchedAsync(earliest.Id, context, DateTime.UtcNow.AddHours(-3));
        await Outbox.MarkDispatchedAsync(dispatched.Id, context, DateTime.UtcNow.AddSeconds(-30));

        // Act
        var message = await Outbox.GetAsync(dispatched.Id, context);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(dispatched.Id, message.Id);
    }

    [Fact]
    public async Task When_Retrieving_Dispatched_Messages_It_Should_Filter_By_Age()
    {
        // Arrange
        var context = new RequestContext();
        var earliest = CreateRandomMessage();
        var dispatched = CreateRandomMessage();
        var undispatched = CreateRandomMessage();
        
        await Outbox.AddAsync([earliest, dispatched, undispatched], context);
        await Outbox.MarkDispatchedAsync(earliest.Id, context, DateTimeOffset.UtcNow.AddHours(-3));
        await Outbox.MarkDispatchedAsync(dispatched.Id, context, DateTime.UtcNow.AddSeconds(-30));
        
        // Act
        var allDispatched = (await Outbox.DispatchedMessagesAsync(TimeSpan.Zero, context)).ToArray();
        var messagesOverAnHour = (await Outbox.DispatchedMessagesAsync(TimeSpan.FromHours(1), context)).ToArray();
        var messagesOver4Hours = (await Outbox.DispatchedMessagesAsync(TimeSpan.FromHours(4), context)).ToArray();
        
        // Assert
        Assert.True(allDispatched.Length >= 2, $"Expecting at least 2 messages, but it got {allDispatched.Length}");
        Assert.Contains(earliest.Id, allDispatched.Select(x => x.Id));
        Assert.Contains(dispatched.Id, allDispatched.Select(x => x.Id));
        Assert.DoesNotContain(undispatched.Id, allDispatched.Select(x => x.Id));
        
        Assert.True(messagesOverAnHour.Length >= 1, "Expecting at least 1 message");
        Assert.Contains(earliest.Id, messagesOverAnHour.Select(x => x.Id));
        Assert.DoesNotContain(dispatched.Id, messagesOverAnHour.Select(x => x.Id));
        Assert.DoesNotContain(undispatched.Id, messagesOverAnHour.Select(x => x.Id));
        
        Assert.DoesNotContain(earliest.Id, messagesOver4Hours.Select(x => x.Id));
        Assert.DoesNotContain(dispatched.Id, messagesOver4Hours.Select(x => x.Id));
        Assert.DoesNotContain(undispatched.Id, messagesOver4Hours.Select(x => x.Id));
    }


    [Fact]
    public async Task When_Retrieving_Outstanding_Messages_It_Should_Filter_By_Age()
    {
        // Arrange
        var context = new RequestContext();
        var earliest = CreateRandomMessage(DateTimeOffset.UtcNow.AddHours(-3));
        var dispatched = CreateRandomMessage();
        var undispatched = CreateRandomMessage();
        
        await Outbox.AddAsync([earliest, dispatched, undispatched], context);
        await Outbox.MarkDispatchedAsync(dispatched.Id, context, DateTime.UtcNow.AddSeconds(-30));

        await Task.Delay(TimeSpan.FromSeconds(10));
        
        // Act
        var allUndispatched = (await Outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).ToArray();
        var messagesOverAnHour = (await Outbox.OutstandingMessagesAsync(TimeSpan.FromHours(1), context)).ToArray();
        var messagesOver4Hours = (await Outbox.OutstandingMessagesAsync(TimeSpan.FromHours(4), context)).ToArray();
        
        // Assert
        Assert.True(allUndispatched.Length >= 2, "Expecting at least 2 messages");
        Assert.Contains(earliest.Id, allUndispatched.Select(x => x.Id));
        Assert.DoesNotContain(dispatched.Id, allUndispatched.Select(x => x.Id));
        Assert.Contains(undispatched.Id, allUndispatched.Select(x => x.Id));
        
        Assert.True(allUndispatched.Length >= 1, "Expecting at least 1 message");
        Assert.Contains(earliest.Id, messagesOverAnHour.Select(x => x.Id));
        Assert.DoesNotContain(dispatched.Id, messagesOverAnHour.Select(x => x.Id));
        Assert.DoesNotContain(undispatched.Id, messagesOverAnHour.Select(x => x.Id));
        
        Assert.DoesNotContain(earliest.Id, messagesOver4Hours.Select(x => x.Id));
        Assert.DoesNotContain(dispatched.Id, messagesOver4Hours.Select(x => x.Id));
        Assert.DoesNotContain(undispatched.Id, messagesOver4Hours.Select(x => x.Id));
    }

    [Fact]
    public async Task When_Retrieving_A_Non_Existent_Message_It_Should_Return_Empty_Message()
    {
        // Arrange
        var context = new RequestContext();
        
        // Act
        var message = await Outbox.GetAsync(Id.Random(), context);
        
        // Assert
        Assert.Equal(MessageType.MT_NONE, message.Header.MessageType);
    }

    [Fact]
    public async Task When_Adding_A_Duplicate_Message_It_Should_Not_Throw()
    {
        // Arrange
        var context = new RequestContext();
        var message = CreateRandomMessage();
        await Outbox.AddAsync(message, context);
        
        // Act
        await Outbox.AddAsync(message, context);
        
        // Assert
        var storedMessage = await Outbox.GetAsync(message.Id, context);
        Assert.Equal(message.Id, storedMessage.Id);
    }

    [Fact]
    public async Task When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties()
    {
        // Arrange
        var message = CreateRandomMessage();
        var context = new RequestContext();
        
        // Act
        await Outbox.AddAsync(message, context);
        var storedMessage = await Outbox.GetAsync(message.Id, context);
        
        // Assert
        Assert.Equal(message.Body.Value, storedMessage.Body.Value);
        
        //should read the header from the sql outbox
        Assert.Equal(message.Header.Topic, storedMessage.Header.Topic);
        Assert.Equal(message.Header.MessageType, storedMessage.Header.MessageType);
        Assert.Equal(message.Header.TimeStamp, storedMessage.Header.TimeStamp, TimeSpan.FromSeconds(1));
        Assert.Equal(0, storedMessage.Header.HandledCount); // -- should be zero when read from outbox
        Assert.Equal(TimeSpan.Zero, storedMessage.Header.Delayed); // -- should be zero when read from outbox
        Assert.Equal(message.Header.CorrelationId, storedMessage.Header.CorrelationId);
        Assert.Equal(message.Header.ReplyTo, storedMessage.Header.ReplyTo);
        Assert.StartsWith(message.Header.ContentType.ToString(), storedMessage.Header.ContentType.ToString());
        Assert.Equal(message.Header.PartitionKey, storedMessage.Header.PartitionKey); 
            
        //Bag serialization
        Assert.Equal(message.Header.Bag.Count,  storedMessage.Header.Bag.Count);
        foreach (var (key, val) in message.Header.Bag)
        {
            Assert.Contains(key, storedMessage.Header.Bag);
            Assert.Equal(val,  storedMessage.Header.Bag[key].ToString());
        }
            
        //Asserts for workflow properties
        Assert.Equal(message.Header.WorkflowId, storedMessage.Header.WorkflowId);
        Assert.Equal(message.Header.JobId, storedMessage.Header.JobId);

        // new fields assertions
        Assert.Equal(message.Header.Source,       storedMessage.Header.Source);
        Assert.Equal(message.Header.Type,         storedMessage.Header.Type);
        Assert.Equal(message.Header.DataSchema,   storedMessage.Header.DataSchema);
        Assert.Equal(message.Header.Subject,      storedMessage.Header.Subject);
        Assert.Equal(message.Header.TraceParent,  storedMessage.Header.TraceParent);
        Assert.Equal(message.Header.TraceState,   storedMessage.Header.TraceState);        
    }
    
    [Fact]
    public virtual async Task When_Adding_A_Message_Within_Transaction_It_Should_Be_Stored_After_Commit()
    {
        // Arrange
        var transaction = CreateTransactionProvider();
        _ = await transaction.GetTransactionAsync();
        
        var message = CreateRandomMessage();
        var context = new RequestContext();
        
        
        // Act
        await Outbox.AddAsync(message, context, transactionProvider: transaction);
        await transaction.CommitAsync();
        
        var storedMessage = await Outbox.GetAsync(message.Id, context);
        
        // Assert
        Assert.Equal(message.Header.MessageType, storedMessage.Header.MessageType);
        Assert.Equal(message.Body.Value, storedMessage.Body.Value);
        
        //should read the header from the sql outbox
        Assert.Equal(message.Header.Topic, storedMessage.Header.Topic);
        Assert.Equal(message.Header.MessageType, storedMessage.Header.MessageType);
        Assert.Equal(message.Header.TimeStamp, storedMessage.Header.TimeStamp, TimeSpan.FromSeconds(1));
        Assert.Equal(0, storedMessage.Header.HandledCount); // -- should be zero when read from outbox
        Assert.Equal(TimeSpan.Zero, storedMessage.Header.Delayed); // -- should be zero when read from outbox
        Assert.Equal(message.Header.CorrelationId, storedMessage.Header.CorrelationId);
        Assert.Equal(message.Header.ReplyTo, storedMessage.Header.ReplyTo);
        Assert.StartsWith(message.Header.ContentType.ToString(), storedMessage.Header.ContentType.ToString());
        Assert.Equal(message.Header.PartitionKey, storedMessage.Header.PartitionKey); 
            
        //Bag serialization
        Assert.Equal(message.Header.Bag.Count,  storedMessage.Header.Bag.Count);
        foreach (var (key, val) in message.Header.Bag)
        {
            Assert.Contains(key, storedMessage.Header.Bag);
            Assert.Equal(val,  storedMessage.Header.Bag[key].ToString());
        }
            
        //Asserts for workflow properties
        Assert.Equal(message.Header.WorkflowId, storedMessage.Header.WorkflowId);
        Assert.Equal(message.Header.JobId, storedMessage.Header.JobId);

        // new fields assertions
        Assert.Equal(message.Header.Source,       storedMessage.Header.Source);
        Assert.Equal(message.Header.Type,         storedMessage.Header.Type);
        Assert.Equal(message.Header.DataSchema,   storedMessage.Header.DataSchema);
        Assert.Equal(message.Header.Subject,      storedMessage.Header.Subject);
        Assert.Equal(message.Header.TraceParent,  storedMessage.Header.TraceParent);
        Assert.Equal(message.Header.TraceState,   storedMessage.Header.TraceState);        
    }
    
     [Fact]
    public virtual async Task When_Adding_A_Message_Within_Transaction_And_Rollback_It_Should_Not_Be_Stored()
    {
        // Arrange
        var transaction = CreateTransactionProvider();
        _ = await transaction.GetTransactionAsync();
        
        var context = new RequestContext();
        var message = CreateRandomMessage();
        
        // Act
        await Outbox.AddAsync(message, context, transactionProvider: transaction);
        await transaction.RollbackAsync();
        var storedMessage = await Outbox.GetAsync(message.Id, context);
        
        // Assert
        Assert.Equal(MessageType.MT_NONE, storedMessage.Header.MessageType);
    }
}
