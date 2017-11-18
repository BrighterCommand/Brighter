using System.IO;
using Newtonsoft.Json;
using ServiceStack;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class BrighterMessageFactory
    {
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
                    //TODO: Log error
                    return message;
                }
                
                var messageHeader = JsonConvert.DeserializeObject<MessageHeader>(reader.ReadLine());
                
                header = reader.ReadLine();
                if (header.TrimStart() != "HEADER/>")
                {
                    //TODO: Log error
                    return message;
                }

                var body = reader.ReadLine();
                if (body.TrimEnd() != "<BODY")
                {
                     //TODO: Log error
                    return message;
                }
                
                var messageBody = new MessageBody(reader.ReadLine());

                body = reader.ReadLine();
                if (body.TrimStart() != "BODY/>")
                {
                    //TODO: Log error
                    return message;
                }
                
                message = new Message(messageHeader, messageBody);

            }


            return message;
        }
    }
}