using Paramore.Brighter.MessagingGateway.Pulsar;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Pulsar.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.Pulsar.Tests.MessagingGateway.Proactor;

// [Trait("Category", "Pulsar")]
// public class BufferedConsumerTestsAsync : IAsyncDisposable
// {
//     private readonly IAmAMessageProducerAsync _messageProducer;
//     private readonly IAmAMessageConsumerAsync _messageConsumer;
//     private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
//
//     public BufferedConsumerTestsAsync()
//     {
//         var connection = GatewayFactory.CreateConnection(); 
//         var publication = new PulsarPublication { Topic = Guid.NewGuid().ToString() }; 
//         var consumer = GatewayFactory.CreateConsumer(connection, publication);
//         var producer = GatewayFactory.CreateProducer(connection,  publication);
//
//         _messageConsumer = new PulsarMessageConsumer(consumer);
//         _messageProducer = new PulsarMessageProducer(producer, publication, TimeProvider.System, InstrumentationOptions.None);
//     }
//
//     [Fact]
//     public async Task When_a_message_consumer_reads_multiple_messages_async()
//     {
//         await _messageConsumer.PurgeAsync();
//         
//         //Post one more than batch size messages
//         var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content One"));
//         await _messageProducer.SendAsync(messageOne);
//         var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Two"));
//         await _messageProducer.SendAsync(messageTwo);
//
//         //let them arrive
//         await Task.Delay(5000);
//         
//         //Now retrieve messages from the consumer
//         var messages = await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));
//
//         // We should get only one message
//         Assert.Single(messages);
//         
//         //ack those to remove from the queue
//         await _messageConsumer.AcknowledgeAsync(messages[0]);
//
//         //Allow ack to register
//         await Task.Delay(1000);
//
//         //Now retrieve again
//         messages = await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(500));
//
//         //This time, just the one message
//         Assert.Single(messages);
//     }
//
//     public async ValueTask DisposeAsync()
//     {
//         await _messageConsumer.PurgeAsync();
//         await _messageProducer.DisposeAsync();
//         await _messageConsumer.DisposeAsync();
//     }
// }
