using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Base.Test.Outbox;

public abstract class OutboxAsyncTest<TTransaction>  
{
    protected abstract IAmAnOutboxAsync<Message, TTransaction> Outbox { get; }

    protected List<Message> CreatedMessages { get; } = [];
    
    [Before(HookType.Test)]    
    public async Task Setup()
    {
        await BeforeEachTestAsync();
    }

    [After(HookType.Test)]
    public async Task Cleanup()
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

    [Test]
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
        
        await Assert.That(messages.Select(x => x.Id)).DoesNotContain(firstMessage.Id);
        await Assert.That(messages.Select(x => x.Id)).Contains(secondMessage.Id);
        await Assert.That(messages.Select(x => x.Id)).Contains(thirdMessage.Id);
    }
    
    [Test]
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
        
        await Assert.That(messages.Select(x => x.Header.MessageId)).DoesNotContain(firstMessage.Id);
        await Assert.That(messages.Select(x => x.Header.MessageId)).DoesNotContain(secondMessage.Id);
        await Assert.That(messages.Select(x => x.Header.MessageId)).DoesNotContain(thirdMessage.Id);
    }

    [Test]
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
        await Assert.That(messages.Length >= 3).IsTrue();
        await Assert.That(messages.Select(x => x.Id)).Contains(earliest.Id);
        await Assert.That(messages.Select(x => x.Id)).Contains(dispatched.Id);
        await Assert.That(messages.Select(x => x.Id)).Contains(undispatched.Id);
    }
    
    [Test]
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
        await Assert.That(messages.Length).IsEqualTo(2);
        await Assert.That(messages.Select(x => x.Id)).Contains(earliest.Id);
        await Assert.That(messages.Select(x => x.Id)).DoesNotContain(dispatched.Id);
        await Assert.That(messages.Select(x => x.Id)).Contains(undispatched.Id);
    }
    
    [Test]
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
        await Assert.That(message).IsNotNull();
        await Assert.That(message.Id).IsEqualTo(dispatched.Id);
    }

    [Test]
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
        await Assert.That(allDispatched.Length >= 2).IsTrue();
        await Assert.That(allDispatched.Select(x => x.Id)).Contains(earliest.Id);
        await Assert.That(allDispatched.Select(x => x.Id)).Contains(dispatched.Id);
        await Assert.That(allDispatched.Select(x => x.Id)).DoesNotContain(undispatched.Id);
        
        await Assert.That(messagesOverAnHour.Length >= 1).IsTrue();
        await Assert.That(messagesOverAnHour.Select(x => x.Id)).Contains(earliest.Id);
        await Assert.That(messagesOverAnHour.Select(x => x.Id)).DoesNotContain(dispatched.Id);
        await Assert.That(messagesOverAnHour.Select(x => x.Id)).DoesNotContain(undispatched.Id);
        
        await Assert.That(messagesOver4Hours.Select(x => x.Id)).DoesNotContain(earliest.Id);
        await Assert.That(messagesOver4Hours.Select(x => x.Id)).DoesNotContain(dispatched.Id);
        await Assert.That(messagesOver4Hours.Select(x => x.Id)).DoesNotContain(undispatched.Id);
    }


    [Test]
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
        await Assert.That(allUndispatched.Length >= 2).IsTrue();
        await Assert.That(allUndispatched.Select(x => x.Id)).Contains(earliest.Id);
        await Assert.That(allUndispatched.Select(x => x.Id)).DoesNotContain(dispatched.Id);
        await Assert.That(allUndispatched.Select(x => x.Id)).Contains(undispatched.Id);
        
        await Assert.That(allUndispatched.Length >= 1).IsTrue();
        await Assert.That(messagesOverAnHour.Select(x => x.Id)).Contains(earliest.Id);
        await Assert.That(messagesOverAnHour.Select(x => x.Id)).DoesNotContain(dispatched.Id);
        await Assert.That(messagesOverAnHour.Select(x => x.Id)).DoesNotContain(undispatched.Id);
        
        await Assert.That(messagesOver4Hours.Select(x => x.Id)).DoesNotContain(earliest.Id);
        await Assert.That(messagesOver4Hours.Select(x => x.Id)).DoesNotContain(dispatched.Id);
        await Assert.That(messagesOver4Hours.Select(x => x.Id)).DoesNotContain(undispatched.Id);
    }

    [Test]
    public async Task When_Retrieving_A_Non_Existent_Message_It_Should_Return_Empty_Message()
    {
        // Arrange
        var context = new RequestContext();
        
        // Act
        var message = await Outbox.GetAsync(Id.Random(), context);
        
        // Assert
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
    }

    [Test]
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
        await Assert.That(storedMessage.Id).IsEqualTo(message.Id);
    }

    [Test]
    public async Task When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties()
    {
        // Arrange
        var message = CreateRandomMessage();
        var context = new RequestContext();
        
        // Act
        await Outbox.AddAsync(message, context);
        var storedMessage = await Outbox.GetAsync(message.Id, context);
        
        // Assert
        await Assert.That(storedMessage.Body.Value).IsEqualTo(message.Body.Value);
        
        //should read the header from the sql outbox
        await Assert.That(storedMessage.Header.Topic).IsEqualTo(message.Header.Topic);
        await Assert.That(storedMessage.Header.MessageType).IsEqualTo(message.Header.MessageType);
        await Assert.That(storedMessage.Header.TimeStamp).IsEqualTo(message.Header.TimeStamp).Within(TimeSpan.FromSeconds(1));
        await Assert.That(storedMessage.Header.HandledCount).IsEqualTo(0); // -- should be zero when read from outbox
        await Assert.That(storedMessage.Header.Delayed).IsEqualTo(TimeSpan.Zero); // -- should be zero when read from outbox
        await Assert.That(storedMessage.Header.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(storedMessage.Header.ReplyTo).IsEqualTo(message.Header.ReplyTo);
        await Assert.That(storedMessage.Header.ContentType.ToString()).StartsWith(message.Header.ContentType.ToString());
        await Assert.That(storedMessage.Header.PartitionKey).IsEqualTo(message.Header.PartitionKey);
            
        //Bag serialization
        await Assert.That(storedMessage.Header.Bag.Count).IsEqualTo(message.Header.Bag.Count);
        foreach (var (key, val) in message.Header.Bag)
        {
            await Assert.That(storedMessage.Header.Bag).ContainsKey(key);
            await Assert.That(storedMessage.Header.Bag[key].ToString()).IsEqualTo(val.ToString());
        }
            
        //Asserts for workflow properties
        await Assert.That(storedMessage.Header.WorkflowId).IsEqualTo(message.Header.WorkflowId);
        await Assert.That(storedMessage.Header.JobId).IsEqualTo(message.Header.JobId);

        // new fields assertions
        await Assert.That(storedMessage.Header.Source).IsEqualTo(message.Header.Source);
        await Assert.That(storedMessage.Header.Type).IsEqualTo(message.Header.Type);
        await Assert.That(storedMessage.Header.DataSchema).IsEqualTo(message.Header.DataSchema);
        await Assert.That(storedMessage.Header.Subject).IsEqualTo(message.Header.Subject);
        await Assert.That(storedMessage.Header.TraceParent).IsEqualTo(message.Header.TraceParent);
        await Assert.That(storedMessage.Header.TraceState).IsEqualTo(message.Header.TraceState);
    }
    
    [Test]
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
        await Assert.That(storedMessage.Header.MessageType).IsEqualTo(message.Header.MessageType);
        await Assert.That(storedMessage.Body.Value).IsEqualTo(message.Body.Value);
        
        //should read the header from the sql outbox
        await Assert.That(storedMessage.Header.Topic).IsEqualTo(message.Header.Topic);
        await Assert.That(storedMessage.Header.MessageType).IsEqualTo(message.Header.MessageType);
        await Assert.That(storedMessage.Header.TimeStamp).IsEqualTo(message.Header.TimeStamp).Within(TimeSpan.FromSeconds(1));
        await Assert.That(storedMessage.Header.HandledCount).IsEqualTo(0); // -- should be zero when read from outbox
        await Assert.That(storedMessage.Header.Delayed).IsEqualTo(TimeSpan.Zero); // -- should be zero when read from outbox
        await Assert.That(storedMessage.Header.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(storedMessage.Header.ReplyTo).IsEqualTo(message.Header.ReplyTo);
        await Assert.That(storedMessage.Header.ContentType.ToString()).StartsWith(message.Header.ContentType.ToString());
        await Assert.That(storedMessage.Header.PartitionKey).IsEqualTo(message.Header.PartitionKey);
            
        //Bag serialization
        await Assert.That(storedMessage.Header.Bag.Count).IsEqualTo(message.Header.Bag.Count);
        foreach (var (key, val) in message.Header.Bag)
        {
            await Assert.That(storedMessage.Header.Bag).ContainsKey(key);
            await Assert.That(storedMessage.Header.Bag[key].ToString()).IsEqualTo(val.ToString());
        }
            
        //Asserts for workflow properties
        await Assert.That(storedMessage.Header.WorkflowId).IsEqualTo(message.Header.WorkflowId);
        await Assert.That(storedMessage.Header.JobId).IsEqualTo(message.Header.JobId);


        // new fields assertions
        await Assert.That(storedMessage.Header.Source).IsEqualTo(message.Header.Source);
        await Assert.That(storedMessage.Header.Type).IsEqualTo(message.Header.Type);
        await Assert.That(storedMessage.Header.DataSchema).IsEqualTo(message.Header.DataSchema);
        await Assert.That(storedMessage.Header.Subject).IsEqualTo(message.Header.Subject);
        await Assert.That(storedMessage.Header.TraceParent).IsEqualTo(message.Header.TraceParent);
        await Assert.That(storedMessage.Header.TraceState).IsEqualTo(message.Header.TraceState);
    }
    
     [Test]
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
        await Assert.That(storedMessage.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
    }
}
