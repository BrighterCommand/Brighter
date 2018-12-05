using System;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    [Collection("AWS")]
    [Trait("Category", "AWS")]
    public class SqsQueuePurgeTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;

        public SqsQueuePurgeTests ()
        {
            MyCommand myCommand = new MyCommand{Value = "Test"};
            
            _message = new Message(
                new MessageHeader(myCommand.Id, "MyCommand", MessageType.MT_COMMAND),
                new MessageBody(JsonConvert.SerializeObject(myCommand))
            );
            
            //Must have credentials stored in the SDK Credentials store or shared credentials file
            if (new CredentialProfileStoreChain().TryGetAWSCredentials("default", out var credentials))
            {
                var awsConnection = new AWSMessagingGatewayConnection(credentials, RegionEndpoint.EUWest1);

                _channelFactory = new ChannelFactory(awsConnection, new SqsMessageConsumerFactory(awsConnection));
                _channel = _channelFactory.CreateChannel(new Connection<MyCommand>());
                
                _messageProducer = new SqsMessageProducer(awsConnection);
            }
        }

        [Fact]
        public void When_purging_the_queue()
        {
            //arange
            _messageProducer.Send(_message);
            _channel.Purge();

            //act
            var message = _channel.Receive(100);
            
            //assert
            message.Header.MessageType.Should().Be(MessageType.MT_NONE);
        }


        public void Dispose()
        {
            var connection = new Connection<MyCommand>();
            _channelFactory.DeleteQueue(connection);
            _channelFactory.DeleteTopic(connection);
        }
    }
}
