using System;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transformers.MassTransit;
using Xunit;

namespace Paramore.Brighter.Transforms.Adaptors.Tests.MassTransit;

public class MassTransitMessageMapperTests
{
    private readonly IRequestContext _context = new RequestContext();
    private readonly Publication _publication = new()
    {
        Topic = "test-topic",
        Source = new Uri("http://source")
    };

    [Fact]
    public void MapToMessage_CommandType_SetsMT_COMMAND()
    {
        var mapper = new MassTransitMessageMapper<TestCommand> { Context = _context };

        var request = new TestCommand();
        var message = mapper.MapToMessage(request, _publication);

        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
    }

    [Fact]
    public void MapToMessage_EventType_SetsMT_EVENT()
    {
        var mapper = new MassTransitMessageMapper<TestEvent> { Context = _context };

        var request = new TestEvent();
        var message = mapper.MapToMessage(request, _publication);

        Assert.Equal(MessageType.MT_EVENT, message.Header.MessageType);
    }

    [Fact]
    public void MapToMessage_OtherType_SetsMT_DOCUMENT()
    {
        var mapper = new MassTransitMessageMapper<TestOtherRequest> { Context = _context };

        var request = new TestOtherRequest();
        var message = mapper.MapToMessage(request, _publication);

        Assert.Equal(MessageType.MT_DOCUMENT, message.Header.MessageType);
    }

    [Fact]
    public void MapToMessage_ContextHasCorrelationId_UsesIt()
    {
        const string correlationId = "test-correlation";
        _context.Bag[MassTransitHeaderNames.CorrelationId] = correlationId;

        var mapper = new MassTransitMessageMapper<TestOtherRequest> { Context = _context };

        var request = new TestOtherRequest();
        var message = mapper.MapToMessage(request, _publication);

        Assert.Equal(correlationId, message.Header.CorrelationId);
    }

    [Fact]
    public void MapToMessage_NoCorrelationIdInContext_GeneratesNew()
    {
        var mapper = new MassTransitMessageMapper<TestOtherRequest> { Context = _context };

        var request = new TestOtherRequest();
        var message = mapper.MapToMessage(request, _publication);

        Assert.NotEqual(Guid.Empty.ToString(), message.Header.CorrelationId.Value);
    }

    [Fact]
    public void MapToRequest_DeserializesEnvelopeCorrectly()
    {
        var expectedId = Guid.NewGuid().ToString();
        var envelope = new MassTransitMessageEnvelop<TestOtherRequest>
        {
            Message = new TestOtherRequest { Id = expectedId },
            MessageId = expectedId,
            CorrelationId = "corr123"
        };

        var bodyBytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonSerialisationOptions.Options);
        var message = new Message(
            new MessageHeader(
                contentType: MassTransitMessageMapper<TestOtherRequest>.MassTransitContentType,
                correlationId: envelope.CorrelationId,
                messageId: envelope.MessageId,
                messageType: MessageType.MT_DOCUMENT,
                timeStamp: DateTimeOffset.UtcNow,
                topic: "test-topic"
            ),
            new MessageBody(bodyBytes, MassTransitMessageMapper<TestOtherRequest>.MassTransitContentType)
        );

        var mapper = new MassTransitMessageMapper<TestOtherRequest>();
        var result = mapper.MapToRequest(message);

        Assert.Equal(expectedId, result.Id);
    }

    [Fact]
    public void MapToMessage_EnvelopeContainsConversationIdFromContext()
    {
        const string conversationId = "conv123";
        _context.Bag[MassTransitHeaderNames.ConversationId] = conversationId;

        var mapper = new MassTransitMessageMapper<TestOtherRequest> { Context = _context };

        var request = new TestOtherRequest();
        var message = mapper.MapToMessage(request, _publication);

        var envelope = JsonSerializer.Deserialize<MassTransitMessageEnvelop<TestOtherRequest>>(message.Body.Bytes, JsonSerialisationOptions.Options);

        Assert.Equal(conversationId, envelope?.ConversationId?.Value);
    }

    [Fact]
    public async Task MapToMessageAsync_CallsMapToMessage()
    {
        var mapper = new MassTransitMessageMapper<TestOtherRequest>();
        var request = new TestOtherRequest();

        var message = await mapper.MapToMessageAsync(request, _publication);

        Assert.NotNull(message);
    }

    [Fact]
    public async Task MapToRequestAsync_CallsMapToRequest()
    {
        var expectedId = Id.Random;
        var envelope = new MassTransitMessageEnvelop<TestOtherRequest>
        {
            Message = new TestOtherRequest
            {
                Id = expectedId
            }
        };

        var bodyBytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonSerialisationOptions.Options);
        var message = new Message(
            new MessageHeader(
                contentType: MassTransitMessageMapper<TestOtherRequest>.MassTransitContentType,
                correlationId: "corr123",
                messageId: expectedId,
                messageType: MessageType.MT_DOCUMENT,
                timeStamp: DateTimeOffset.UtcNow,
                topic: "test-topic"
            ),
            new MessageBody(bodyBytes, MassTransitMessageMapper<TestOtherRequest>.MassTransitContentType)
        );

        var mapper = new MassTransitMessageMapper<TestOtherRequest>();
        var result = await mapper.MapToRequestAsync(message);

        Assert.Equal(expectedId, result.Id);
    }
}

// Sample request types for testing
public class TestCommand() :Command(Guid.NewGuid())
{
    public string Name { get; set; } = string.Empty;
}

public class TestEvent() : Event(Guid.NewGuid())
{
    public string Name { get; set; } = string.Empty;
}

public class TestOtherRequest : IRequest
{
    public Id? CorrelationId { get; set; }
    public Id Id { get; set; } = Id.Random;
    public string Name { get; set; } = string.Empty;
}
