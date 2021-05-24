
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")]
    [Trait("Fragile", "CI")]
    public class SqsMessageProducerDlqTests : IDisposable
    {
        private readonly SqsMessageProducer _sender;
        private readonly IAmAChannel _channel;
        private readonly ChannelFactory _channelFactory;
        private readonly Message _message;
        private readonly AWSMessagingGatewayConnection _awsConnection;
        private readonly string _dlqChannelName;

        public SqsMessageProducerDlqTests ()
        {
            MyCommand myCommand = new MyCommand{Value = "Test"};
            Guid correlationId = Guid.NewGuid();
            string replyTo = "http:\\queueUrl";
            string contentType = "text\\plain";
            var channelName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _dlqChannelName =$"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);
            
            SqsSubscription<MyCommand> subscription = new SqsSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                redrivePolicy: new RedrivePolicy(_dlqChannelName, 2)
            );
            
            _message = new Message(
                new MessageHeader(myCommand.Id, topicName, MessageType.MT_COMMAND, correlationId, replyTo, contentType),
                new MessageBody(JsonSerializer.Serialize((object) myCommand, JsonSerialisationOptions.Options))
            );
 
            //Must have credentials stored in the SDK Credentials store or shared credentials file
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            _awsConnection = new AWSMessagingGatewayConnection(credentials, region);
            
            _sender = new SqsMessageProducer(_awsConnection, new SqsPublication{MakeChannels = OnMissingChannel.Create});
            
            _sender.ConfirmTopicExists(topicName);
            
            //We need to do this manually in a test - will create the channel from subscriber parameters
            _channelFactory = new ChannelFactory(_awsConnection);
            _channel = _channelFactory.CreateChannel(subscription);
        }

        [Fact]
        public void When_requeueing_redrives_to_the_queue()
        { 
            _sender.Send(_message);
             var receivedMessage = _channel.Receive(5000); 
            _channel.Requeue(receivedMessage );

            receivedMessage = _channel.Receive(5000);
            _channel.Requeue(receivedMessage );
            
            //should force us into the dlq
            receivedMessage = _channel.Receive(5000);
            _channel.Requeue(receivedMessage) ;

            Task.Delay(5000);
            
            //inspect the dlq
            GetDLQCount(_dlqChannelName).Should().Be(1);
        }

        public void Dispose()
        {
            _channelFactory.DeleteTopic();
            _channelFactory.DeleteQueue();
        }

        public int GetDLQCount(string queueName)
        {
            using(var sqsClient = new AmazonSQSClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                var queueUrlResponse = sqsClient.GetQueueUrlAsync(queueName).GetAwaiter().GetResult();
                var response = sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrlResponse.QueueUrl,
                    WaitTimeSeconds = 5,
                    AttributeNames = new List<string> { "ApproximateReceiveCount" },
                    MessageAttributeNames = new List<string> { "All" }
                }).GetAwaiter().GetResult();

                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new AmazonSQSException($"Failed to GetMessagesAsync for queue {queueName}. Response: {response.HttpStatusCode}");
                }
                
                return response.Messages.Count;

            }
           
        }

    }
}
