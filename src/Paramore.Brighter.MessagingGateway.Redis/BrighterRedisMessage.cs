using System.IO;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class BrighterRedisMessage
    {
        public static RedisValue Write(Message message)
        {
            var buffer = new StringBuilder();
            var writer = new StringWriter(buffer);
            //TODO: We need to be wire compatible with Kombu here
            //WriteHeader(message);
            //WriteBody(message);
            return message.Body.Value;
        }

        public static async Task<RedisValue> WriteAsync(Message message)
        {
           return "";
        }

        public static Message Read(RedisValue value)
        {
            return new Message(new MessageHeader(),new MessageBody(value));
        }
    }
}