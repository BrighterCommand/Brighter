using Newtonsoft.Json;
using Paramore.Brighter;
using Tasks.Ports.Events;

namespace TasksApi.MessageMappers
{
    public class TaskCompletedEventMapper : IAmAMessageMapper<TaskCompletedEvent>

    {
        public Message MapToMessage(TaskCompletedEvent request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "Task.Completed", messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        public TaskCompletedEvent MapToRequest(Message message)
        {
            throw new System.NotImplementedException();
        }
    }
}