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
using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal sealed class MyCommandMessageMapper : IAmAMessageMapper<MyCommand>
    {
        public IRequestContext Context { get; set; }

        public Message MapToMessage(MyCommand request, Publication publication)
        {
            var header = new MessageHeader(
                messageId: request.Id, 
                topic: publication.Topic ?? RoutingKey.Empty, 
                messageType: request.RequestToMessageType(), 
                source: publication.Source,
                type: publication.Type,
                correlationId: request.CorrelationId,
                timeStamp: DateTimeOffset.UtcNow,
                replyTo: publication.ReplyTo ?? RoutingKey.Empty,
                contentType: new ContentType(MediaTypeNames.Application.Json){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()},
                partitionKey: Context.GetPartitionKey(),
                dataSchema: publication.DataSchema,
               subject: publication.Subject,
                workflowId: Context.GetWorkflowId(),
                jobId: Context.GetJobId()
        );
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
        }

        public MyCommand MapToRequest(Message message)
        {
            var command = JsonSerializer.Deserialize<MyCommand>(message.Body.Value, JsonSerialisationOptions.Options);
            return command;
        }
    }
}
