using Newtonsoft.Json;
using paramore.brighter.commandprocessor;

namespace TaskMailer.Ports
{
    internal class TaskReminderCommandMessageMapper : IAmAMessageMapper<TaskReminderCommand>
    {
        public Message MapToMessage(TaskReminderCommand request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "Task.Reminder", messageType: MessageType.MT_COMMAND);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        public TaskReminderCommand MapToRequest(Message message)
        {
            return JsonConvert.DeserializeObject<TaskReminderCommand>(message.Body.Value);
        }
    }
}