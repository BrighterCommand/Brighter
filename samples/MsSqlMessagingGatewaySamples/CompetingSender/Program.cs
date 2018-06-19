using System;
using System.Transactions;
using Events;
using Events.Adapters.ServiceHost;
using Events.Ports.Commands;
using Events.Ports.Mappers;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.MsSql;

namespace CompetingSender
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("usage: MultipleSender <count>");
                Console.WriteLine("eg   : MultipleSender 500");
                return;
            }

            if (!int.TryParse(args[0], out int repeatCount))
            {
                Console.WriteLine($"{args[0]} is not a valid number");
                return;
            }

            var container = new TinyIoCContainer();

            var messageMapperFactory = new TinyIoCMessageMapperFactory(container);

            var messageMapperRegistry = new MessageMapperRegistry(messageMapperFactory)
            {
                {typeof(CompetingConsumerCommand), typeof(CompetingConsumerCommandMessageMapper)}
            };

            var messageStore = new InMemoryMessageStore();

            var messagingConfiguration =
                new MsSqlMessagingGatewayConfiguration(
                    @"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;", "QueueData");
            var producer = new MsSqlMessageProducer(messagingConfiguration);

            var builder = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration())
                .DefaultPolicy()
                .TaskQueues(new MessagingConfiguration((IAmAMessageStore<Message>) messageStore, producer, messageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();

            using (new TransactionScope(TransactionScopeOption.RequiresNew,
                new TransactionOptions {IsolationLevel = IsolationLevel.ReadCommitted},
                TransactionScopeAsyncFlowOption.Enabled))
            {
                Console.WriteLine($"Sending {repeatCount} command messages");
                var sequenceNumber = 1;
                for (int i = 0; i < repeatCount; i++)
                {
                    commandProcessor.Post(new CompetingConsumerCommand(sequenceNumber++));
                }
                // We do NOT complete the transaction here to show that a message is
                // always queued, whether the transaction commits or aborts!
            }
        }
    }
}
