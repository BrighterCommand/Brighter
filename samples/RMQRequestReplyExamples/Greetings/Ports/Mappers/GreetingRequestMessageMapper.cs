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
using System.Text.Json;
using Greetings.Ports.Commands;
using Paramore.Brighter;

namespace Greetings.Ports.Mappers
{
    public class GreetingRequestMessageMapper : IAmAMessageMapper<GreetingRequest>
    {
        public Message MapToMessage(GreetingRequest request)
        {
            var header = new MessageHeader(
                messageId: request.Id, 
                topic: "Greeting.Request", 
                messageType:MessageType.MT_COMMAND,
                correlationId: request.ReplyAddress.CorrelationId,
                replyTo: request.ReplyAddress.Topic);

            var body = new MessageBody(JsonSerializer.Serialize(new GreetingsRequestBody(request.Id.ToString(), request.Name, request.Language), JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
 
        }

        public GreetingRequest MapToRequest(Message message)
        {
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var command = new GreetingRequest(replyAddress);
            var body = JsonSerializer.Deserialize<GreetingsRequestBody>(message.Body.Value, JsonSerialisationOptions.Options);
            command.Name = body.Name;
            command.Language = body.Language;
            return command;
        }
    }
}
