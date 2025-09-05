#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    internal sealed class KafkaMessagePublisher(
        IProducer<string, byte[]> producer,
        IKafkaMessageHeaderBuilder headerBuilder)
    {
        public void PublishMessage(Message message, Action<DeliveryReport<string, byte[]>> deliveryReport)
        {
            var kafkaMessage = BuildMessage(message);
            
            producer.Produce(message.Header.Topic, kafkaMessage, deliveryReport);
        }

        public async Task PublishMessageAsync(Message message, Action<DeliveryResult<string, byte[]>> deliveryReport, CancellationToken cancellationToken = default)
        {
            var kafkaMessage = BuildMessage(message);

            try
            { 
                var deliveryResult = await producer.ProduceAsync(message.Header.Topic, kafkaMessage, cancellationToken);
                deliveryReport(deliveryResult);
            }
            catch (ProduceException<string, byte[]>)
            {
                //unlike the sync path, async will throw if it can't write. We want to capture that exception and raise the event
                //so that our flows match
                DeliveryResult<string, byte[]> deliveryResult = new();
                deliveryResult.Status = PersistenceStatus.NotPersisted;
                deliveryResult.Message = new Message<string, byte[]>();
                deliveryResult.Message.Headers = [];
                deliveryResult.Headers.Add(HeaderNames.MESSAGE_ID, message.Id.Value.ToByteArray());
                deliveryReport(deliveryResult);
            }

        }

        private Message<string, byte[]> BuildMessage(Message message)
        {
            var headers = headerBuilder.Build(message);

            var kafkaMessage = new Message<string, byte[]>
            {
                Headers = headers,
                Key = message.Header.PartitionKey,
                Value = message.Body.Bytes
            };

            return kafkaMessage;
        }
    }
}
