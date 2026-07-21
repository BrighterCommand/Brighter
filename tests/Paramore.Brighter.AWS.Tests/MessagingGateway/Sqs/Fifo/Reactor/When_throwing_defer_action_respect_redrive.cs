using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Fifo.Reactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class SnsReDrivePolicySDlqTests : IAsyncDisposable
{
    private readonly IAmAMessagePump _messagePump;
    private readonly Message _message;
    private readonly string _dlqChannelName;
    private readonly IAmAChannelSync _channel;
    private readonly SqsMessageProducer _sender;
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly SqsSubscription<MyCommand> _subscription;
    private readonly ChannelFactory _channelFactory;

    public SnsReDrivePolicySDlqTests()
    {
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        _dlqChannelName = $"Redrive-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Redrive-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Redrive-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey(queueName);

        var channelName = new ChannelName(queueName);
        var queueAttributes = new SqsAttributes(
            redrivePolicy: new RedrivePolicy(
                new ChannelName(_dlqChannelName), 2),
            type: SqsType.Fifo,
            tags: new Dictionary<string, string> { { "Environment", "Test" } });
        
        _subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            requeueCount: -1,
            requeueDelay: TimeSpan.FromMilliseconds(50),
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: queueAttributes, makeChannels: OnMissingChannel.Create);

        var myCommand = new MyDeferredCommand { Value = "Hello Redrive" };
        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: messageGroupId),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        _awsConnection = GatewayFactory.CreateFactory();

        _sender = new SqsMessageProducer(
            _awsConnection,
            new SqsPublication(
                    channelName: channelName, 
                    queueAttributes: queueAttributes,
                    makeChannels: OnMissingChannel.Create
                )
        );

        _channelFactory = new ChannelFactory(_awsConnection);
        _channel = _channelFactory.CreateSyncChannel(_subscription);

        IHandleRequests<MyDeferredCommand> handler = new MyDeferredCommandHandler();

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyDeferredCommand, MyDeferredCommandHandler>();

        IAmACommandProcessor commandProcessor = new CommandProcessor(
            subscriberRegistry: subscriberRegistry,
            handlerFactory: new QuickHandlerFactory(() => handler),
            requestContextFactory: new InMemoryRequestContextFactory(),
            policyRegistry: new PolicyRegistry(),
            resilienceResiliencePipelineRegistry: new ResiliencePipelineRegistry<string>(),
            requestSchedulerFactory: new InMemorySchedulerFactory()
        );

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyDeferredCommandMessageMapper()),
            null
        );
        messageMapperRegistry.Register<MyDeferredCommand, MyDeferredCommandMessageMapper>();

        _messagePump = new ServiceActivator.Reactor(commandProcessor, (message) => typeof(MyDeferredCommand), messageMapperRegistry,
            new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), _channel)
        {
            Channel = _channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = 3
        };
    }

    public async Task<int> GetDLQCountAsync(string queueName)
    {
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrlResponse = await sqsClient.GetQueueUrlAsync(queueName.ToValidSQSQueueName(true));
        var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrlResponse.QueueUrl,
            WaitTimeSeconds = 5,
            MessageSystemAttributeNames = ["ApproximateReceiveCount"],
            MessageAttributeNames = new List<string> { "All" }
        });

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new AmazonSQSException(
                $"Failed to GetMessagesAsync for queue {queueName}. Response: {response.HttpStatusCode}");
        }

        return response.Messages.Count;
    }

    [Test, Skip("This test is skipped because running tests of the DLQ is unreliable in the CI environment")]
    public async Task When_throwing_defer_action_respect_redrive_async()
    {
        await _sender.SendAsync(_message);

        var task = Task.Factory.StartNew(() => _messagePump.Run(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.Delay(5000);

        var quitMessage = MessageFactory.CreateQuitMessage(_subscription.RoutingKey);
        _channel.Enqueue(quitMessage);

        await task;

        await Task.Delay(5000);

        var dlqCount = await GetDLQCountAsync(_dlqChannelName);
        await Assert.That(dlqCount).IsEqualTo(1);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
