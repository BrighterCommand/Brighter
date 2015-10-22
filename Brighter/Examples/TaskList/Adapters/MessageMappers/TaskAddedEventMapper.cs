﻿using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using Tasks.Ports.Events;

namespace Tasklist.Adapters.MessageMappers
{
    public class TaskAddedEventMapper : IAmAMessageMapper<TaskAddedEvent>
    {
        public Message MapToMessage(TaskAddedEvent request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "Task.Added", messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        public TaskAddedEvent MapToRequest(Message message)
        {
            throw new System.NotImplementedException();
        }
    }
}