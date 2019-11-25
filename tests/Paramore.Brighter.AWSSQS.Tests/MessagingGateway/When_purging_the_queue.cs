using System;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    [Collection("AWS")]
    [Trait("Category", "AWS")]
    public class SqsQueuePurgeTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private Connection<MyCommand> _connection = new Connection<MyCommand>(channelName: new ChannelName($"{typeof(MyCommand)}.{Guid.NewGuid()}"));

        public SqsQueuePurgeTests ()
        {
            MyCommand myCommand = new MyCommand{Value = "Test"};
            
            _message = new Message(
                new MessageHeader(myCommand.Id, "MyCommand", MessageType.MT_COMMAND),
                new MessageBody(JsonConvert.SerializeObject((object) myCommand))
            );
            
            var credentialChain = new CredentialProfileStoreChain();
            
            if (credentialChain.TryGetAWSCredentials("default", out var credentials) && credentialChain.TryGetProfile("default", out var profile))
            {
                var awsConnection = new AWSMessagingGatewayConnection(credentials, profile.Region);

                _channelFactory = new ChannelFactory(awsConnection, new SqsMessageConsumerFactory(awsConnection));
                _channel = _channelFactory.CreateChannel(_connection);
                
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
            _channelFactory.DeleteQueue(_connection);
            _channelFactory.DeleteTopic(_connection);
        }
    }
}
