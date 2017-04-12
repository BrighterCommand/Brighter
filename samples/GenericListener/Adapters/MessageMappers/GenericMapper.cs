using System;
using System.Text;
using GenericListener.Ports.Events;
using Newtonsoft.Json.Linq;
using Paramore.Brighter;

namespace GenericListener.Adapters.MessageMappers
{
    public class GenericMapper<T> : IAmAMessageMapper<T> where T : EventStoredEvent, new()
    {
        private readonly string _context;

        public GenericMapper()
        {
            _context = typeof(T).Name;
        }

        public GenericMapper(string context)
        {
            _context = context;
        }

        public Message MapToMessage(T request)
        {
            throw new System.NotImplementedException();
        }

        public T MapToRequest(Message message)
        {
            var data = JObject.Parse(message.Body.Value);

            // I have chosen to use TaskId here but likely this will be just "Id" or some kind of common Id property.
            if (!data.HasProperty("TaskId"))
            {
                throw new Exception(
                    string.Format("Unable to locate TaskId property '{2}' on type '{0}' via '{1}' (might need custom type implementation){3}{4}", 
                        _context, 
                        message.Header.Topic, 
                        message.Header.MessageType,
                        Environment.NewLine,
                        message.Body.Value));
            }

            var taskid = data.SelectToken("TaskId");

            return new T
            {
                Id = message.Id,
                Context = "Task", // _context
                JsonData = Encoding.UTF8.GetBytes(message.Body.Value),
                CategoryId = taskid.ToString(),
                MetaData = new
                {
                    MessageTimeStamp = message.Header.TimeStamp,
                    MessageType = message.Header.MessageType.ToString(),
                    MessageTopic = message.Header.Topic
                }
            };
        }
    }
}