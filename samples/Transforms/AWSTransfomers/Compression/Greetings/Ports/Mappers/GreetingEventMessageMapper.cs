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

using System.IO.Compression;
using System.Text.Json;
using Greetings.Ports.Commands;
using Paramore.Brighter;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Transforms.Attributes;
using Paramore.Brighter.Transforms.Transformers;

namespace Greetings.Ports.Mappers
{
    public class GreetingEventMessageMapper : IAmAMessageMapper<GreetingEvent>
    {
        public IRequestContext Context { get; set; }
        
        //Although SNS allows 256K, we have to use UTF8 strings, which is not the format of a compressed message
        //so we have to convert the bytes to base64 and then use that as the message body
        //this inflates the byte count, see https://stackoverflow.com/questions/54224029/aws-sns-publising-compressed-payload
        [Compress(0, CompressionMethod.GZip, CompressionLevel.Optimal, 150)]
        public Message MapToMessage(GreetingEvent request, Publication publication)
        {
            var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
        }

        [Decompress(0, CompressionMethod.GZip)]
        public GreetingEvent MapToRequest(Message message)
        {
            var greetingCommand = JsonSerializer.Deserialize<GreetingEvent>(message.Body.Value, JsonSerialisationOptions.Options);
            
            return greetingCommand;
        }
    }
}
