using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")]
    [Trait("Fragile", "CI")]
    public class AWSValidateInfrastructureByConventionTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAMessageConsumer _consumer;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;

        private readonly Message _fifoMessage;
        private readonly IAmAMessageConsumer _fifoConsumer;
        private readonly SqsMessageProducer _fifoMessageProducer;
        private readonly ChannelFactory _fifoChannelFactory;

        private readonly MyCommand _myCommand;

        public AWSValidateInfrastructureByConventionTests()
        {
            _myCommand = new MyCommand { Value = "Test" };
            string correlationId = Guid.NewGuid().ToString();
            const string replyTo = "http:\\queueUrl";
            const string contentType = "text\\plain";
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);

            SqsSubscription<MyCommand> subscription = new(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                makeChannels: OnMissingChannel.Create
            );

            _message = new Message(
                new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                    replyTo: new RoutingKey(replyTo), contentType: contentType),
                new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
            );


            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            //We need to do this manually in a test - will create the channel from subscriber parameters
            //This doesn't look that different from our create tests - this is because we create using the channel factory in
            //our AWS transport, not the consumer (as it's a more likely to use infrastructure declared elsewhere)
            _channelFactory = new ChannelFactory(awsConnection);
            var channel = _channelFactory.CreateChannel(subscription);

            //Now change the subscription to validate, just check what we made - will make the SNS Arn to prevent ListTopics call
            subscription = new(
                name: new SubscriptionName(channelName),
                channelName: channel.Name,
                routingKey: routingKey,
                findTopicBy: TopicFindBy.Convention,
                makeChannels: OnMissingChannel.Validate
            );

            _messageProducer = new SqsMessageProducer(
                awsConnection,
                new SnsPublication { FindTopicBy = TopicFindBy.Convention, MakeChannels = OnMissingChannel.Validate }
            );

            _consumer = new SqsMessageConsumerFactory(awsConnection).Create(subscription);

            // Because fifo can modify the topic name we need to run the same test twice, one for standard queue and another to FIFO
            var fifoChannelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string fifoTopicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var fifoRoutingKey = new RoutingKey(fifoTopicName);

            SqsSubscription<MyCommand> fifoSubscription = new(
                name: new SubscriptionName(fifoChannelName),
                channelName: new ChannelName(fifoChannelName),
                routingKey: fifoRoutingKey,
                makeChannels: OnMissingChannel.Create
            );

            _fifoMessage = new Message(
                new MessageHeader(_myCommand.Id, fifoRoutingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                    replyTo: new RoutingKey(replyTo), contentType: contentType,
                    partitionKey: Guid.NewGuid().ToString()),
                new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
            );

            //We need to do this manually in a test - will create the channel from subscriber parameters
            //This doesn't look that different from our create tests - this is because we create using the channel factory in
            //our AWS transport, not the consumer (as it's a more likely to use infrastructure declared elsewhere)
            _fifoChannelFactory = new ChannelFactory(awsConnection);
            var fifoChannel = _fifoChannelFactory.CreateChannel(fifoSubscription);

            //Now change the subscription to validate, just check what we made - will make the SNS Arn to prevent ListTopics call
            fifoSubscription = new(
                name: new SubscriptionName(fifoChannelName),
                channelName: fifoChannel.Name,
                routingKey: fifoRoutingKey,
                findTopicBy: TopicFindBy.Convention,
                makeChannels: OnMissingChannel.Validate,
                sqsType: SnsSqsType.Fifo
            );

            _messageProducer = new SqsMessageProducer(
                awsConnection,
                new SnsPublication
                {
                    FindTopicBy = TopicFindBy.Convention,
                    MakeChannels = OnMissingChannel.Validate,
                    SnsType = SnsSqsType.Fifo
                }
            );

            _fifoConsumer = new SqsMessageConsumerFactory(awsConnection).Create(fifoSubscription);
        }

        [Fact]
        public async Task When_infrastructure_exists_can_verify()
        {
            //arrange
            _messageProducer.Send(_message);

            await Task.Delay(1000);

            var messages = _consumer.Receive(TimeSpan.FromMilliseconds(5000));

            //Assert
            var message = messages.First();
            message.Id.Should().Be(_myCommand.Id);

            //clear the queue
            _consumer.Acknowledge(message);
        }

        [Fact]
        public async Task When_infrastructure_exists_can_verify_for_fico()
        {
            //arrange
            _fifoMessageProducer.Send(_fifoMessage);

            await Task.Delay(1000);

            var messages = _fifoConsumer.Receive(TimeSpan.FromMilliseconds(5000));

            //Assert
            var message = messages.First();
            message.Id.Should().Be(_myCommand.Id);

            //clear the queue
            _fifoConsumer.Acknowledge(message);
        }

        public void Dispose()
        {
            _channelFactory.DeleteTopic();
            _channelFactory.DeleteQueue();
            _consumer.Dispose();
            _messageProducer.Dispose();

            _fifoChannelFactory.DeleteTopic();
            _fifoChannelFactory.DeleteQueue();
            _fifoConsumer.Dispose();
            _fifoMessageProducer.Dispose();
        }
    }
}
