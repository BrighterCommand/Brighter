using System;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessagePublication : Publication
    {
        //placeholder
    }

    public class RedisMessagePublication<T> : RedisMessagePublication
        where T: class, IRequest
    {
        public override Type? RequestType { get; set; } = typeof(T);
    }
}
