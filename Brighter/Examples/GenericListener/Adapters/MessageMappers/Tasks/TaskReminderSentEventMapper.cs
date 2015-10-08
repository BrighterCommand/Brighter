using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using Tasks.Ports.Events;

namespace GenericListener.Adapters.MessageMappers.Tasks
{
    public class TaskReminderSentEventMapper : IAmAMessageMapper<TaskReminderSentEvent>
    {
        public Message MapToMessage(TaskReminderSentEvent request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "Task.ReminderSent", messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        public TaskReminderSentEvent MapToRequest(Message message)
        {
            return JsonConvert.DeserializeObject<TaskReminderSentEvent>(message.Body.Value);
        }
    }
}