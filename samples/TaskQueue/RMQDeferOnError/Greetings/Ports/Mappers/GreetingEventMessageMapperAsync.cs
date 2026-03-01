#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Commands;
using Paramore.Brighter;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Greetings.Ports.Mappers;

public class GreetingEventMessageMapperAsync : IAmAMessageMapperAsync<GreetingEvent>
{
    public IRequestContext Context { get; set; }

    public async Task<Message> MapToMessageAsync(GreetingEvent request, Publication publication, CancellationToken cancellationToken = default)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: request.RequestToMessageType());
        var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, request, JsonSerialisationOptions.Options, cancellationToken);
        var body = new MessageBody(ms.ToArray());
        var message = new Message(header, body);
        return message;
    }

    public async Task<GreetingEvent> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream(message.Body.Bytes);
        var greetingCommand = await JsonSerializer.DeserializeAsync<GreetingEvent>(ms, JsonSerialisationOptions.Options, cancellationToken);
        return greetingCommand;
    }
}
