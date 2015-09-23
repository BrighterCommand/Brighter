#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using paramore.brighter.commandprocessor;
using Tasks.Ports.Commands;

namespace Tasks.Ports
{
    public class TaskReminderCommandMessageMapper : IAmAMessageMapper<TaskReminderCommand>
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
            var data = JObject.Parse(message.Body.Value);

            var taskName = (string)data.SelectToken("TaskName");
            var dueDate = ConvertDeserializedDateToDateTime((JValue)data.SelectToken("DueDate"));
            var recipient = (string)data.SelectToken("Recipient");
            var copyTo = (string)data.SelectToken("CopyTo");

            return new TaskReminderCommand(taskName, dueDate, recipient, copyTo);
        }

        private static DateTime ConvertDeserializedDateToDateTime(JValue jvalue)
        {
            DateTime dateTime;

            if (jvalue.Value is string)
            {
                dateTime = DateTime.Parse(jvalue.Value as string);
            }
            else
            {
                dateTime = (DateTime)jvalue.Value;
            }

            if (dateTime.Kind != DateTimeKind.Utc)
            {
                dateTime = dateTime.ToUniversalTime();
            }

            return dateTime;
        }
    }
}