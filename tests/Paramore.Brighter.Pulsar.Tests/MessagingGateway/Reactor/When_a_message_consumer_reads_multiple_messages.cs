using Paramore.Brighter.MessagingGateway.Pulsar;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Pulsar.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.Pulsar.Tests.MessagingGateway.Reactor;

// [Trait("Category", "Pulsar")]
// public class BufferedConsumerTests : IDisposable
// {
//     private readonly IAmAMessageProducerSync _messageProducer;
//     private readonly IAmAMessageConsumerSync _messageConsumer;
//     private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
//     private const int BatchSize = 3;
//
//     public BufferedConsumerTests()
//     {
//         var connection = GatewayFactory.CreateConnection(); 
//         var publication = new PulsarPublication { Topic = Guid.NewGuid().ToString() };
//         var consumer = GatewayFactory.CreateConsumer(connection, publication);
//         var producer = GatewayFactory.CreateProducer(connection, publication);
//
//         _messageConsumer = new PulsarMessageConsumer(consumer);
//         _messageProducer = new PulsarMessageProducer(producer, publication, TimeProvider.System, InstrumentationOptions.None);
//     }
//
//     [Fact]
//     public void When_a_message_consumer_reads_multiple_messages()
//     {
//         _messageConsumer.Purge();
//         //Post one more than batch size messages
//         var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content One"));
//         _messageProducer.Send(messageOne);
//         var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Two"));
//         _messageProducer.Send(messageTwo);
//
//         //let them arrive
//         Thread.Sleep(5000);
//
//         //Now retrieve messages from the consumer
//         var messages = _messageConsumer.Receive(TimeSpan.FromMilliseconds(1000));
//
//         // We should get only one message
//         Assert.Single(messages);
//         
//         //ack those to remove from the queue
//         _messageConsumer.Acknowledge(messages[0]);
//
//         //Allow ack to register
//         Thread.Sleep(1000);
//
//         //Now retrieve again
//         messages = _messageConsumer.Receive(TimeSpan.FromMilliseconds(500));
//
//         //This time, just the one message
//         Assert.Single(messages);
//     }
//
//     public void Dispose()
//     {
//         _messageConsumer.Purge();
//         _messageProducer.Dispose();
//         _messageConsumer.Dispose();
//     }
// }
