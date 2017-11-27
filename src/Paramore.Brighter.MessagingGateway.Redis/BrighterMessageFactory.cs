using System;
using System.IO;
using Newtonsoft.Json;
using Paramore.Brighter.MessagingGateway.Redis.LibLog;
using ServiceStack;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class BrighterMessageFactory
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<BrighterMessageFactory>);
        
        public Message Create(string redisMessage)
        {
            var message = new Message();
            if (redisMessage.IsNullOrEmpty())
            {
                return message;
            }
            
            using (var reader = new StringReader(redisMessage))
            {
                var header = reader.ReadLine();
                if (header.TrimEnd() != "<HEADER")
                {
                    _logger.Value.ErrorFormat("Expected message to begin with <HEADER, but was {0}", redisMessage);
                    return message;
                }
                
                var messageHeader = JsonConvert.DeserializeObject<MessageHeader>(reader.ReadLine());
                
                header = reader.ReadLine();
                if (header.TrimStart() != "HEADER/>")
                {
                    _logger.Value.ErrorFormat("Expected message to find end of HEADER/>, but was {0}", redisMessage);
                    return message;
                }

                var body = reader.ReadLine();
                if (body.TrimEnd() != "<BODY")
                {
                    _logger.Value.ErrorFormat("Expected message to have beginning of <BODY, but was {0}", redisMessage);
                    return message;
                }
                
                var messageBody = new MessageBody(reader.ReadLine());

                body = reader.ReadLine();
                if (body.TrimStart() != "BODY/>")
                {
                    _logger.Value.ErrorFormat("Expected message to find end of BODY/>, but was {0}", redisMessage);
                    return message;
                }
                
                message = new Message(messageHeader, messageBody);

            }

            return message;
        }
    }
}