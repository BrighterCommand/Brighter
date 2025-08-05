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

using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal sealed class MyCommandMessageMapperAsync : IAmAMessageMapperAsync<MyCommand>
    {
        public IRequestContext Context { get; set; }

        public async Task<Message> MapToMessageAsync(MyCommand request, Publication publication, CancellationToken cancellationToken = default)
        {
            using MemoryStream stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, request, JsonSerialisationOptions.Options, cancellationToken);
            var header = new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), type: publication.Type);
            var body = new MessageBody(stream.ToArray());
            return new Message(header, body);
        }

        public async Task<MyCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream(message.Body.Bytes);
            stream.Position = 0;
            var command = await JsonSerializer.DeserializeAsync<MyCommand>(stream, JsonSerialisationOptions.Options, cancellationToken);
            return command;
        }
    }
}
