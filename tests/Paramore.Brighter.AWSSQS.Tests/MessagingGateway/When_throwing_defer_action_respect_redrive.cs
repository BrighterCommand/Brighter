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
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")]
    [Trait("Fragile", "CI")]
     public class SnsReDrivePolicySDlqTests
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly Message _message;
        private readonly string _dlqChannelName;
        private readonly ChannelFactory _channelFactory;
        private readonly IAmAChannel _channel;
        private readonly SqsMessageProducer _sender;
        private readonly string _topicName;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly AWSMessagingGatewayConnection _awsConnection;

        public SnsReDrivePolicySDlqTests()
        {
            Guid correlationId = Guid.NewGuid();
            string replyTo = "http:\\queueUrl";
            string contentType = "text\\plain";
            var channelName = $"Redrive-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _dlqChannelName = $"Redrive-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _topicName = $"Redrive-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(_topicName);

            //how are we consuming
            var subscription = new SqsSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                //don't block the redrive policy from owning retry management
                requeueCount: -1,
                //delay before requeuing
                requeueDelayInMs: 50,
                //we want our SNS subscription to manage requeue limits using the DLQ for 'too many requeues'
                redrivePolicy: new RedrivePolicy
                (
                    deadLetterQueueName: new ChannelName(_dlqChannelName),
                    maxReceiveCount: 2
                ));

            //what do we send
            var myCommand = new MyDeferredCommand { Value = "Hello Redrive" };
            _message = new Message(
                new MessageHeader(myCommand.Id, _topicName, MessageType.MT_COMMAND, correlationId, replyTo, contentType),
                new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
            );

            //Must have credentials stored in the SDK Credentials store or shared credentials file
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            _awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            //how do we send to the queue
            _sender = new SqsMessageProducer(_awsConnection, new SnsPublication { MakeChannels = OnMissingChannel.Create });

            //We need to do this manually in a test - will create the channel from subscriber parameters
            _channelFactory = new ChannelFactory(_awsConnection);
            _channel = _channelFactory.CreateChannel(subscription);

            //how do we handle a command
            IHandleRequests<MyDeferredCommand> handler = new MyDeferredCommandHandler();

            //hook up routing for the command processor
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyDeferredCommand, MyDeferredCommandHandler>();

            //once we read, how do we dispatch to a handler. N.B. we don't use this for reading here
            _commandProcessor = new CommandProcessor(
                subscriberRegistry: subscriberRegistry,
                handlerFactory: new QuickHandlerFactory(() => handler),
                requestContextFactory: new InMemoryRequestContextFactory(),
                policyRegistry: new PolicyRegistry()
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyDeferredCommandMessageMapper(_topicName))
                ); 
            messageMapperRegistry.Register<MyDeferredCommand, MyDeferredCommandMessageMapper>();
            
            //pump messages from a channel to a handler - in essence we are building our own dispatcher in this test
            _messagePump = new MessagePumpBlocking<MyDeferredCommand>(_commandProcessor, messageMapperRegistry)
            {
                Channel = _channel, TimeoutInMilliseconds = 5000, RequeueCount = 3
            };
        }

        public int GetDLQCount(string queueName)
        {
            using (var sqsClient = new AmazonSQSClient(_awsConnection.Credentials, _awsConnection.Region))
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


        [Fact]
        public void When_throwing_defer_action_respect_redrive()
        {
            //put something on an SNS topic, which will be delivered to our SQS queue
            _sender.Send(_message);

            //start a message pump, let it process messages
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(5000).Wait();

            //send a quit message to the pump to terminate it 
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            _channel.Enqueue(quitMessage);

            //wait for the pump to stop once it gets a quit message
            Task.WaitAll(new[] { task });
            
            Task.Delay(5000);

            //inspect the dlq
            GetDLQCount(_dlqChannelName).Should().Be(1);
        }
    }
}
