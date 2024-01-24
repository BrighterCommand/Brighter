using System;
using System.Text.Json;

namespace Paramore.Brighter
{
    public class JsonMessageMapper<T> : BaseMessageMapper<T> where T : class, IRequest
    {
        public JsonMessageMapper(IRequestContext requestContext, RoutingKey routingKey = null,
            Func<T, string> routingKeyFunc = null) : base(requestContext, routingKey, routingKeyFunc)
        {
        }

        protected override MessageBody CreateMessageBody(T request)
        {
            return new MessageBody(JsonSerializer.SerializeToUtf8Bytes(request, JsonSerialisationOptions.Options), "JSON");
        }

        protected override T CreateType(Message message)
        {
            return JsonSerializer.Deserialize<T>(message.Body.Bytes, JsonSerialisationOptions.Options);
        }
    }
}
