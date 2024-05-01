using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.MessageMappers;

    public class JsonMessageMapper<T> : IAmAMessageMapper<T> where T : class, IRequest
    {
        public Message MapToMessage(T request, Publication publication)
        {
            var messageHeader = new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), DateTime.UtcNow, contentType: "application/json");

            return new Message(messageHeader, new MessageBody(JsonSerializer.SerializeToUtf8Bytes(request, JsonSerialisationOptions.Options), "JSON"));
        }

        public T MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<T>(message.Body.Bytes, JsonSerialisationOptions.Options);
        }

    }

    public class JsonMessageMapperAsync<T> : IAmAMessageMapperAsync<T> where T : class, IRequest
    {
        public async Task<Message> MapToMessageAsync(T request, Publication publication, CancellationToken cancellationToken = default)
        {
            using MemoryStream stream = new();
            await JsonSerializer.SerializeAsync(stream, request, new JsonSerializerOptions(JsonSerialisationOptions.Options), cancellationToken);
            return new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), DateTime.UtcNow, contentType: "application/json"),
                new MessageBody(stream.ToArray()));
        }

        public async Task<T> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream(message.Body.Bytes);
            stream.Position = 0;
            return await JsonSerializer.DeserializeAsync<T>(stream,JsonSerialisationOptions.Options, cancellationToken:cancellationToken);
        }
    }
    
