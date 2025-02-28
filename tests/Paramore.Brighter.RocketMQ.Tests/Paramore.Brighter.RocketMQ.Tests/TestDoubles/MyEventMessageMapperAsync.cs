namespace Paramore.Brighter.RocketMQ.Tests.TestDoubles;

internal class MyEventMessageMapperAsync : IAmAMessageMapperAsync<MyEvent>
{
    public IRequestContext Context { get; set; }
        
    public Task<Message> MapToMessageAsync(MyEvent request, Publication publication, CancellationToken cancellationToken = default)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: MessageType.MT_EVENT);
        var body = new MessageBody(request.ToString());
        var message = new Message(header, body);
        return Task.FromResult(message);
    }

    public Task<MyEvent> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)  
    {
        var myEvent = new MyEvent { Id = message.Id };
        return Task.FromResult(myEvent);
    }
}
