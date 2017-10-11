#region Licence
/* The MIT License (MIT)
Copyright © 2017 Wayne Hunsley <whunsley@gmail.com>

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
using System.Linq;
using KafkaTaskQueueSamples.Greetings.TinyIoc;
using KafkaTaskQueueSamples.Greetings.Adapters.ServiceHost;
using KafkaTaskQueueSamples.Greetings.Ports.Commands;
using KafkaTaskQueueSamples.Greetings.Ports.Mappers;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace KafkaTaskQueueSamples.GreetingsSender
{
    class Program
    {
        static void Main(string[] args)
        {
            var kafkaMessagingGatewayConfiguration
                = new KafkaMessagingGatewayConfiguration()
                {
                    Name = "paramore.brighter.greetingsender",
                    BootStrapServers = new[] {"localhost:9092"}
                };

            for (var i = 0; i < args.Length; i += 2)
            {
                var key = args[i];
                var val = default(string);
                if (i + 1 < args.Length)
                {
                    val = args[i + 1];
                }

                switch (key)
                {
                    case "--bootstrap-server":
                        kafkaMessagingGatewayConfiguration.BootStrapServers = new[] {val};
                        break;
                    default:
                        break;
                }
            }

            var container = new TinyIoCContainer();
            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);
            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof(GreetingEvent), typeof(GreetingEventMessageMapper)}
            };

            var messageStore = new InMemoryMessageStore();
            var producer = new KafkaMessageProducerFactory(kafkaMessagingGatewayConfiguration)
                               .Create();

            var builder = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration())
                .DefaultPolicy()
                .TaskQueues(new MessagingConfiguration(messageStore, producer, messageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();
            commandProcessor.Post(new GreetingEvent("Wayne"));
        }
    }
}