using System;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Proactor;

[Trait("Category", "AWS")]
public class SqsMessageProducerCreateQueueWithMaximumMessageSizeAsyncTests : IAsyncDisposable, IDisposable
{
    private const int OneMebibyte = 1_048_576;

    private readonly SqsMessageProducer _messageProducer;
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly string _queueName;
    private readonly Message _message;

    public SqsMessageProducerCreateQueueWithMaximumMessageSizeAsyncTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        _queueName = $"Producer-Size-Tests-{Guid.NewGuid()}".Truncate(45);
        var channelName = new ChannelName(_queueName);

        _message = new Message(
            new MessageHeader(myCommand.Id, new RoutingKey(_queueName), MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        _awsConnection = GatewayFactory.CreateFactory();

        _messageProducer = new SqsMessageProducer(
            _awsConnection,
            new SqsPublication
            {
                ChannelName = channelName,
                MakeChannels = OnMissingChannel.Create,
                QueueAttributes = new SqsAttributes(maximumMessageSize: OneMebibyte)
            });
    }

    [Fact]
    public async Task When_creating_a_queue_with_maximum_message_size_async()
    {
        //arrange
        await _messageProducer.SendAsync(_message);

        //act
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrl = (await sqsClient.GetQueueUrlAsync(_queueName)).QueueUrl;
        var queueAttributes = await sqsClient.GetQueueAttributesAsync(
            new GetQueueAttributesRequest { QueueUrl = queueUrl, AttributeNames = [QueueAttributeName.MaximumMessageSize] });

        //assert
        Assert.Equal("1048576", queueAttributes.Attributes[QueueAttributeName.MaximumMessageSize]);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrl = (await sqsClient.GetQueueUrlAsync(_queueName)).QueueUrl;
        await sqsClient.DeleteQueueAsync(queueUrl);

        await _messageProducer.DisposeAsync();
    }
}
