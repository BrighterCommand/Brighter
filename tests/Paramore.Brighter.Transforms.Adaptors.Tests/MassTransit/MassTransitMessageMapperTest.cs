using System;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transformers.MassTransit;

namespace Paramore.Brighter.Transforms.Adaptors.Tests.MassTransit;

public class MassTransitMessageMapperTests
{
    private readonly IRequestContext _context = new RequestContext();
    private readonly Publication _publication = new()
    {
        Topic = "test-topic",
        Source = new Uri("http://source")
    };

    [Test]
    public async Task MapToMessage_CommandType_SetsMT_COMMAND()
    {
        var mapper = new MassTransitMessageMapper<TestCommand> { Context = _context };

        var request = new TestCommand();
        var message = await mapper.MapToMessageAsync(request, _publication);

        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
    }

    [Test]
    public async Task MapToMessage_EventType_SetsMT_EVENT()
    {
        var mapper = new MassTransitMessageMapper<TestEvent> { Context = _context };

        var request = new TestEvent();
        var message = await mapper.MapToMessageAsync(request, _publication);

        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_EVENT);
    }

    [Test]
    public async Task MapToMessage_OtherType_SetsMT_DOCUMENT()
    {
        var mapper = new MassTransitMessageMapper<TestOtherRequest> { Context = _context };

        var request = new TestOtherRequest();
        var message = await mapper.MapToMessageAsync(request, _publication);

        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_DOCUMENT);
    }

    [Test]
    public async Task MapToMessage_ContextHasCorrelationId_UsesIt()
    {
        const string correlationId = "test-correlation";
        _context.Bag[MassTransitHeaderNames.CorrelationId] = correlationId;

        var mapper = new MassTransitMessageMapper<TestOtherRequest> { Context = _context };

        var request = new TestOtherRequest();
        var message = await mapper.MapToMessageAsync(request, _publication);

        await Assert.That(message.Header.CorrelationId).IsEqualTo(correlationId);
    }

    [Test]
    public async Task MapToMessage_NoCorrelationIdInContext_GeneratesNew()
    {
        var mapper = new MassTransitMessageMapper<TestOtherRequest> { Context = _context };

        var request = new TestOtherRequest();
        var message = await mapper.MapToMessageAsync(request, _publication);

        await Assert.That(message.Header.CorrelationId.Value).IsNotEqualTo(Guid.Empty.ToString());
    }

    [Test]
    public async Task MapToRequest_DeserializesEnvelopeCorrectly()
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
        var result = await mapper.MapToRequestAsync(message);

        await Assert.That(result.Id).IsEqualTo(expectedId);
    }

    [Test]
    public async Task MapToMessage_EnvelopeContainsConversationIdFromContext()
    {
        const string conversationId = "conv123";
        _context.Bag[MassTransitHeaderNames.ConversationId] = conversationId;

        var mapper = new MassTransitMessageMapper<TestOtherRequest> { Context = _context };

        var request = new TestOtherRequest();
        var message = await mapper.MapToMessageAsync(request, _publication);

        var envelope = JsonSerializer.Deserialize<MassTransitMessageEnvelop<TestOtherRequest>>(message.Body.Bytes, JsonSerialisationOptions.Options);

        await Assert.That(envelope?.ConversationId?.Value).IsEqualTo(conversationId);
    }

    [Test]
    public async Task MapToMessageAsync_CallsMapToMessage()
    {
        var mapper = new MassTransitMessageMapper<TestOtherRequest>();
        var request = new TestOtherRequest();

        var message = await mapper.MapToMessageAsync(request, _publication);

        await Assert.That(message).IsNotNull();
    }

    [Test]
    public async Task MapToRequestAsync_CallsMapToRequest()
    {
        var expectedId = Id.Random();
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

        await Assert.That(result.Id).IsEqualTo(expectedId);
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
    public Id Id { get; set; } = Id.Random();
    public string Name { get; set; } = string.Empty;
}
