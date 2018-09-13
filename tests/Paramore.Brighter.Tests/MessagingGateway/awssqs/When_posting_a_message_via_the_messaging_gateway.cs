using System;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SQS;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    [Trait("Category", "AWS")]
    public class SqsMessageProeducerSendTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly InputChannelFactory _channelFactory;

        public SqsMessageProeducerSendTests()
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

                _channelFactory = new InputChannelFactory(awsConnection, new SqsMessageConsumerFactory(awsConnection));
                _channel = _channelFactory.CreateInputChannel(new Connection<MyCommand>());
                
                _messageProducer = new SqsMessageProducer(awsConnection);
            }
        }


        [Fact]
        public void When_posting_a_message_via_the_producer()
        {
            //arrange
            _messageProducer.Send(_message);
            
            var message =_channel.Receive(2000);

            //should_send_the_message_to_aws_sqs
            message.Body.Should().NotBeNull();
        }

        public void Dispose()
        {
            //Clean up resources that we have created

            var connection = new Connection<MyCommand>();
            _channelFactory.DeleteQueue(connection);
            _channelFactory.DeleteTopic(connection);
        }
    }
}
