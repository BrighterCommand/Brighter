using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Base.Test.Outbox;

public abstract class OutboxTest<TTransaction>
{
    protected abstract IAmAnOutboxSync<Message, TTransaction> Outbox { get; }

    protected List<Message> CreatedMessages { get; } = [];
    
    protected OutboxTest()
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        BeforeEachTest();
    }

    protected virtual void BeforeEachTest()
    {
        CreateStore();
    }
    
    protected virtual void CreateStore()
    {
    }
    
    public void Dispose()
    {
        AfterEachTest();
    }

    protected virtual void AfterEachTest()
    {
        DeleteStore();
    }

    protected virtual void DeleteStore()
    {
    }

    protected abstract IEnumerable<Message> GetAllMessages();
    
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
    public void RemovingOneMessage()
    {
        // arrange
        var context = new RequestContext();
        var firstMessage = CreateRandomMessage();
        var secondMessage = CreateRandomMessage();
        var thirdMessage = CreateRandomMessage();
        
        // Act
        Outbox.Add(firstMessage, context);
        Outbox.Add(secondMessage, context);
        Outbox.Add(thirdMessage, context);
        
        Outbox.Delete([firstMessage.Id], context);
        
        // Assert
        var messages = Outbox
            .OutstandingMessages(TimeSpan.Zero, context)
            .ToArray();
        
        Assert.Equal(2, messages.Length);
        Assert.Contains(secondMessage.Id, messages.Select(x => x.Id));
        Assert.Contains(thirdMessage.Id, messages.Select(x => x.Id));
    }
    
    [Fact]
    public void RemovingMultipleMessages()
    {
        // arrange
        var context = new RequestContext();
        var firstMessage = CreateRandomMessage();
        var secondMessage = CreateRandomMessage();
        var thirdMessage = CreateRandomMessage();
        
        // Act
        Outbox.Add(firstMessage, context);
        Outbox.Add(secondMessage, context);
        Outbox.Add(thirdMessage, context);
        
        Outbox.Delete([firstMessage.Id, secondMessage.Id, thirdMessage.Id], context);
        
        // Assert
        var messages = Outbox
            .OutstandingMessages(TimeSpan.Zero, context)
            .ToArray();
        
        Assert.Empty(messages);
    }

    [Fact]
    public void RetrievingMessages()
    {
        // Arrange
        var context = new RequestContext();
        var earliest = CreateRandomMessage();
        var dispatched = CreateRandomMessage();
        var undispatched = CreateRandomMessage();
        
        Outbox.Add([earliest, dispatched, undispatched], context);
        Outbox.MarkDispatched(earliest.Id, context, DateTime.UtcNow.AddHours(-3));
        Outbox.MarkDispatched(dispatched.Id, context);
        
        // Act
        var messages = GetAllMessages().ToArray();

        // Assert
        Assert.True(messages.Length >= 3, "Expecting at least 3 messages");
        Assert.Contains(earliest.Id, messages.Select(x => x.Id));
        Assert.Contains(dispatched.Id, messages.Select(x => x.Id));
        Assert.Contains(undispatched.Id, messages.Select(x => x.Id));
    }
    
    [Fact]
    public void RetrievingMessagesByIds()
    {
        // Arrange
        var context = new RequestContext();
        var earliest = CreateRandomMessage();
        var dispatched = CreateRandomMessage();
        var undispatched = CreateRandomMessage();
        
        Outbox.Add([earliest, dispatched, undispatched], context);
        Outbox.MarkDispatched(earliest.Id, context, DateTime.UtcNow.AddHours(-3));
        Outbox.MarkDispatched(dispatched.Id, context);
        
        // Act
        var messages =  Outbox
            .Get([earliest.Id, undispatched.Id], context)
            .ToArray();

        // Assert
        Assert.Equal(2, messages.Length);
        Assert.Contains(earliest.Id, messages.Select(x => x.Id));
        Assert.DoesNotContain(dispatched.Id, messages.Select(x => x.Id));
        Assert.Contains(undispatched.Id, messages.Select(x => x.Id));
    }
    
    [Fact]
    public void RetrievingMessagesById()
    {
        // Arrange
        var context = new RequestContext();
        var earliest = CreateRandomMessage();
        var dispatched = CreateRandomMessage();
        var undispatched = CreateRandomMessage();
        
        Outbox.Add([earliest, dispatched, undispatched], context);
        Outbox.MarkDispatched(earliest.Id, context, DateTime.UtcNow.AddHours(-3));
        Outbox.MarkDispatched(dispatched.Id, context);
        
        // Act
        var message = Outbox.Get(dispatched.Id, context);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(dispatched.Id, message.Id);
    }

    [Fact]
    public void RetrievingDispatchedMessages()
    {
        // Arrange
        var context = new RequestContext();
        var earliest = CreateRandomMessage();
        var dispatched = CreateRandomMessage();
        var undispatched = CreateRandomMessage();
        
        Outbox.Add([earliest, dispatched, undispatched], context);
        Outbox.MarkDispatched(earliest.Id, context, DateTime.UtcNow.AddHours(-3));
        Outbox.MarkDispatched(dispatched.Id, context);
        
        // Act
        var allDispatched = Outbox.DispatchedMessages(TimeSpan.Zero, context).ToArray();
        var messagesOverAnHour  = Outbox.DispatchedMessages(TimeSpan.FromHours(1), context).ToArray();
        var messagesOver4Hours   = Outbox.DispatchedMessages(TimeSpan.FromHours(4), context).ToArray();
        
        // Assert
        Assert.True(allDispatched.Length >= 2, "Expecting at least 2 messages");
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
    public void RetrievingNotDispatchedMessage()
    {
        // Arrange
        var context = new RequestContext();
        var earliest = CreateRandomMessage(DateTimeOffset.UtcNow.AddHours(-3));
        var dispatched = CreateRandomMessage();
        var undispatched = CreateRandomMessage();
        
        Outbox.Add([earliest, dispatched, undispatched], context);
        Outbox.MarkDispatched(dispatched.Id, context);
        
        // Act
        var allUndispatched = Outbox.OutstandingMessages(TimeSpan.Zero, context).ToArray();
        var messagesOverAnHour = Outbox.OutstandingMessages(TimeSpan.FromHours(1), context).ToArray();
        var messagesOver4Hours = Outbox.OutstandingMessages(TimeSpan.FromHours(4), context).ToArray();
        
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
    public void RetrievingAMessageThatNotExists()
    {
        // Arrange
        var context = new RequestContext();
        
        // Act
        var message = Outbox.Get(Id.Random(), context);
        
        // Assert
        Assert.Equal(MessageType.MT_NONE, message.Header.MessageType);
    }

    [Fact]
    public void IgnoreDuplicatedMessage()
    {
        // Arrange
        var context = new RequestContext();
        var message = CreateRandomMessage();
        Outbox.Add(message, context);
        
        // Act
        Outbox.Add(message, context);
        
        // Assert
        // Just adding a simple assertion to remove any warning
        Assert.True(true);
    }

    [Fact]
    public void AddMessage()
    {
        // Arrange
        var context = new RequestContext();
        var message = CreateRandomMessage();
        
        // Act
        Outbox.Add(message, context);
        var storedMessage = Outbox.Get(message.Id, context);
        
        // Assertion
        Assert.Equal(message.Body.Value, storedMessage.Body.Value);
        
        //should read the header from the sql outbox
        Assert.Equal(message.Header.Topic, storedMessage.Header.Topic);
        Assert.Equal(message.Header.MessageType, storedMessage.Header.MessageType);
        Assert.Equal(message.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"), storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"));
        Assert.Equal(0, storedMessage.Header.HandledCount); // -- should be zero when read from outbox
        Assert.Equal(TimeSpan.Zero, storedMessage.Header.Delayed); // -- should be zero when read from outbox
        Assert.Equal(message.Header.CorrelationId, storedMessage.Header.CorrelationId);
        Assert.Equal(message.Header.ReplyTo, storedMessage.Header.ReplyTo);
        Assert.Equal(message.Header.ContentType, storedMessage.Header.ContentType);
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
    public void AddMessageUsingTransaction()
    {
        // Arrange
        var transaction = CreateTransactionProvider();
        _ = transaction.GetTransaction();
        
        var message = CreateRandomMessage();
        var context = new RequestContext();
        
        
        // Act
        Outbox.Add(message, context, transactionProvider: transaction);
        transaction.Commit();
        
        var storedMessage = Outbox.Get(message.Id, context);
        
        // Assertion
        Assert.Equal(message.Body.Value, storedMessage.Body.Value);
        
        //should read the header from the sql outbox
        Assert.Equal(message.Header.Topic, storedMessage.Header.Topic);
        Assert.Equal(message.Header.MessageType, storedMessage.Header.MessageType);
        Assert.Equal(message.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"), storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"));
        Assert.Equal(0, storedMessage.Header.HandledCount); // -- should be zero when read from outbox
        Assert.Equal(TimeSpan.Zero, storedMessage.Header.Delayed); // -- should be zero when read from outbox
        Assert.Equal(message.Header.CorrelationId, storedMessage.Header.CorrelationId);
        Assert.Equal(message.Header.ReplyTo, storedMessage.Header.ReplyTo);
        Assert.Equal(message.Header.ContentType, storedMessage.Header.ContentType);
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
    public void AddMessageUsingTransactionShouldNotInsertWhenRollback()
    {
        // Arrange
        var transaction = CreateTransactionProvider();
        _ = transaction.GetTransaction();
        
        var context = new RequestContext();
        var message = CreateRandomMessage();
        
        // Act
        Outbox.Add(message, context, transactionProvider: transaction);
        transaction.Rollback();
        var storedMessage = Outbox.Get(message.Id, context);
        
        // Assertion
        Assert.Equal(MessageType.MT_NONE, storedMessage.Header.MessageType);
    }
}
