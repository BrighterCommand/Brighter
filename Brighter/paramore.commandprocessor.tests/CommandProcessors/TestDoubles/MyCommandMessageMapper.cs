using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyCommandMessageMapper : IAmAMessageMapper<MyCommand, Message>
    {
        public Message Map(MyCommand request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "MyCommand");
            var body = new MessageBody(string.Format("id:{0}, value:{1} ", request.Id, request.Value));
            var message = new Message(header, body);
            return message;
        }
    }
}