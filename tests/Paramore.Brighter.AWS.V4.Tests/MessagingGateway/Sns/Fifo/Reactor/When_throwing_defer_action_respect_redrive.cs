using System;
using System.Net;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWS.V4;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Fifo.Reactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SnsReDrivePolicySDlqTests : IDisposable, IAsyncDisposable
{
    private readonly IAmAMessagePump _messagePump;
    private readonly Message _message;
    private readonly string _dlqChannelName;
    private readonly IAmAChannelSync _channel;
    private readonly SnsMessageProducer _sender;
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly SqsSubscription<MyCommand> _subscription;
    private readonly ChannelFactory _channelFactory;

    public SnsReDrivePolicySDlqTests()
    {
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        _dlqChannelName = $"Redrive-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var correlationId = Id.Random();
        var channelName = $"Redrive-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var topicName = $"Redrive-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey(topicName);
        var topicAttributes = new SnsAttributes { Type = SqsType.Fifo };

        //how are we consuming
        _subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            //don't block the redrive policy from owning retry management
            requeueCount: -1,
            //delay before requeuing
            requeueDelay: TimeSpan.FromMilliseconds(50),
            messagePumpType: MessagePumpType.Reactor,
            //we want our SNS subscription to manage requeue limits using the DLQ for 'too many requeues'
            queueAttributes: new SqsAttributes(
                redrivePolicy: new RedrivePolicy(new ChannelName(_dlqChannelName)!, 2),
                type: SqsType.Fifo),
            topicAttributes: topicAttributes,
            makeChannels: OnMissingChannel.Create
            );

        //what do we send
        var myCommand = new MyDeferredCommand { Value = "Hello Redrive" };
        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: messageGroupId),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        //Must have credentials stored in the SDK Credentials store or shared credentials file
        _awsConnection = GatewayFactory.CreateFactory();

        //how do we send to the queue
        _sender = new SnsMessageProducer(
            _awsConnection,
            new SnsPublication
            {
                Topic = routingKey,
                RequestType = typeof(MyDeferredCommand),
                MakeChannels = OnMissingChannel.Create,
                TopicAttributes = topicAttributes
            }
        );

        //We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = new ChannelFactory(_awsConnection);
        _channel = _channelFactory.CreateSyncChannel(_subscription);

        //how do we handle a command
        IHandleRequests<MyDeferredCommand> handler = new MyDeferredCommandHandler();

        //hook up routing for the command processor
        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyDeferredCommand, MyDeferredCommandHandler>();

        //once we read, how do we dispatch to a handler. N.B. we don't use this for reading here
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

        //pump messages from a channel to a handler - in essence we are building our own dispatcher in this test
        _messagePump = new ServiceActivator.Reactor(commandProcessor, (message) => typeof(MyDeferredCommand), messageMapperRegistry,
            null, new InMemoryRequestContextFactory(), _channel)
        {
            Channel = _channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = 3
        };
    }

    private int GetDLQCount(string queueName)
    {
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrlResponse = sqsClient.GetQueueUrlAsync(queueName).GetAwaiter().GetResult();
        var response = sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrlResponse.QueueUrl,
            WaitTimeSeconds = 5,
            MessageSystemAttributeNames = ["ApproximateReceiveCount"],
            MessageAttributeNames = ["All"]
        }).GetAwaiter().GetResult();

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new AmazonSQSException(
                $"Failed to GetMessagesAsync for queue {queueName}. Response: {response.HttpStatusCode}");
        }

        return response.Messages.Count;
    }


    [Fact]
    public async Task When_throwing_defer_action_respect_redrive()
    {
        //put something on an SNS topic, which will be delivered to our SQS queue
        _sender.Send(_message);

        //start a message pump, let it process messages
        var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
        await Task.Delay(5000);

        //send a quit message to the pump to terminate it 
        var quitMessage = MessageFactory.CreateQuitMessage(_subscription.RoutingKey);
        _channel.Enqueue(quitMessage);

        //wait for the pump to stop once it gets a quit message
        await Task.WhenAll(task);

        await Task.Delay(5000);

        //inspect the dlq
        Assert.Equal(1, GetDLQCount(_dlqChannelName + ".fifo"));
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
