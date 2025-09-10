#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4.Extensions;
using Paramore.Brighter.Tasks;
using InvalidOperationException = System.InvalidOperationException;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

public class AwsMessagingGateway(AWSMessagingGatewayConnection awsConnection)
{
    protected static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AwsMessagingGateway>();

    private readonly AWSClientFactory _awsClientFactory = new(awsConnection);
    protected readonly AWSMessagingGatewayConnection AwsConnection = awsConnection;

    /// <summary>
    /// The Channel Address
    /// The Channel Address can be a Topic ARN or Queue Url
    /// </summary>
    protected string? ChannelAddress => ChannelTopicArn ?? ChannelQueueUrl;

    /// <summary>
    /// The Channel Topic Arn
    /// </summary>
    protected string? ChannelTopicArn { get; set; }

    /// <summary>
    /// The Channel Queue URL
    /// </summary>
    protected string? ChannelQueueUrl { get; set; }

    /// <summary>
    /// The Channel Dead Letter Queue ARN
    /// </summary>
    protected string? ChannelDeadLetterQueueArn { get; set; }


    protected async Task<string?> EnsureQueueAsync(
        string queue,
        ChannelType channelType,
        QueueFindBy findQueueBy,
        SqsAttributes? sqsAttributes,
        OnMissingChannel makeChannel = OnMissingChannel.Create,
        CancellationToken cancellationToken = default)
    {
        if (sqsAttributes is null)
            throw new ConfigurationException("No SQS attributes provided for channel");

        ChannelQueueUrl = makeChannel switch
        {
            //on validate or assume, turn a routing key into a queueUrl
            OnMissingChannel.Assume or OnMissingChannel.Validate => 
                await ValidateQueueAsync(queue, findQueueBy, sqsAttributes.Type, makeChannel, cancellationToken),
            OnMissingChannel.Create => 
                await CreateQueueAsync(queue, sqsAttributes, cancellationToken),
            _ => ChannelQueueUrl
        };

        return ChannelQueueUrl;
    }
    
    protected RoutingKey EnsureSubscription(
        bool isFifo,
        string queueUrl,
        RoutingKey routingKey,
        TopicFindBy findTopicBy,
        SnsAttributes? snsAttributes,
        SqsAttributes? sqsAttributes,
        OnMissingChannel makeChannels = OnMissingChannel.Create)
    => BrighterAsyncContext.Run(async () => await EnsureSubscriptionAsync(isFifo, queueUrl, routingKey, findTopicBy, snsAttributes, sqsAttributes, makeChannels));
    

    protected async Task<RoutingKey> EnsureSubscriptionAsync(
        bool isFifo,
        string queueUrl,
        RoutingKey routingKey,
        TopicFindBy findTopicBy,
        SnsAttributes? snsAttributes,
        SqsAttributes? sqsAttributes,
        OnMissingChannel makeChannels = OnMissingChannel.Create,
        CancellationToken ct = default)
    {
        var topicAttributes = snsAttributes ?? new SnsAttributes();
        var queueAttributes = sqsAttributes ?? new SqsAttributes();
        var topic = routingKey.ToValidSNSTopicName(isFifo);

        await EnsureTopicAsync(
            topic,
            findTopicBy,
            topicAttributes,
            makeChannels,
            ct);

        await CheckQueueSubscribedAsync(
            queueUrl,
            queueAttributes,
            makeChannels,
            ct
        );
        return routingKey;
    }

    protected async Task<string?> EnsureTopicAsync(
        RoutingKey topic,
        TopicFindBy topicFindBy,
        SnsAttributes? attributes,
        OnMissingChannel makeTopic = OnMissingChannel.Create,
        CancellationToken cancellationToken = default)
    {
        var type = attributes?.Type ?? SqsType.Standard;
        ChannelTopicArn = makeTopic switch
        {
            //on validate or assume, turn a routing key into a topicARN
            OnMissingChannel.Assume or OnMissingChannel.Validate => 
                await ValidateTopicAsync(topic, topicFindBy, type, cancellationToken),
            OnMissingChannel.Create =>
                await CreateTopicAsync(topic, attributes),
            _ => ChannelAddress
        };

        return ChannelTopicArn;
    }
    
    private async Task CheckQueueSubscribedAsync(
        string queueUrl,
        SqsAttributes? sqsAttributes,
        OnMissingChannel makeChannel = OnMissingChannel.Create,
        CancellationToken cancellationToken = default)
    {
        using var snsClient = _awsClientFactory.CreateSnsClient();
        using var sqsClient = _awsClientFactory.CreateSqsClient();
        await CheckSubscriptionAsync(makeChannel, ChannelTopicArn!, queueUrl, sqsAttributes, sqsClient, snsClient, cancellationToken);
    }

    private async Task CheckSubscriptionAsync(OnMissingChannel makeSubscriptions,
        string topicArn,
        string queueUrl,
        SqsAttributes? sqsAttributes,
        AmazonSQSClient sqsClient,
        AmazonSimpleNotificationServiceClient snsClient, CancellationToken cancellationToken)
    {
        if (makeSubscriptions == OnMissingChannel.Assume)
        {
            return;
        }

        if (!await SubscriptionExistsAsync(topicArn, queueUrl, sqsClient, snsClient, cancellationToken))
        {
            if (makeSubscriptions == OnMissingChannel.Validate)
            {
                throw new BrokerUnreachableException(
                    $"Subscription validation error: could not find subscription for {queueUrl}");
            }

            if (makeSubscriptions == OnMissingChannel.Create)
            {
                await SubscribeToTopicAsync(topicArn, queueUrl, sqsAttributes, sqsClient, snsClient, cancellationToken);
            }
        }
    }

    private async Task<string> CreateTopicAsync(RoutingKey topic, SnsAttributes? snsAttributes)
    {
        snsAttributes ??= SnsAttributes.Empty;
        
        using var snsClient = _awsClientFactory.CreateSnsClient();

        var topicName = topic.Value;
        
        if (snsAttributes.Type == SqsType.Fifo)
        {
            topicName = topic.ToValidSNSTopicName(true);
        }

        Dictionary<string, string?> attributes = CreateTopicAttributes(snsAttributes);

        var createTopicRequest = new CreateTopicRequest(topicName) { Attributes = attributes, Tags = [new Tag { Key = "Source", Value = "Brighter" }] };

        //create topic is idempotent, so safe to call even if topic already exists
        var createTopic = await snsClient.CreateTopicAsync(createTopicRequest);
        if (!string.IsNullOrEmpty(createTopic.TopicArn))
            return createTopic.TopicArn;

        throw new InvalidOperationException(
            $"Could not create Topic topic: {topic} on {AwsConnection.Region}");
    }

    private async Task<string> CreateQueueAsync(
        string queueName,
        SqsAttributes? sqsAttributes,
        CancellationToken cancellationToken)
    {
        sqsAttributes ??= SqsAttributes.Empty;

        if (sqsAttributes.RedrivePolicy != null)
            ChannelDeadLetterQueueArn = await CreateDeadLetterQueueAsync(sqsAttributes, cancellationToken);

        using var sqsClient = _awsClientFactory.CreateSqsClient();

        queueName = queueName.ToValidSQSQueueName(sqsAttributes.Type == SqsType.Fifo);

        Dictionary<string, string?> attributes = CreateQueueAttributes(sqsAttributes);
        Dictionary<string, string> tags = CreateQueueTags(sqsAttributes);

        string queueUrl;
        var createQueueRequest = new CreateQueueRequest(queueName) { Attributes = attributes, Tags = tags };
        try
        {
            // create queue is idempotent, so safe to call even if queue already exists
            var createQueueResponse = await sqsClient.CreateQueueAsync(createQueueRequest, cancellationToken);
            queueUrl = createQueueResponse.QueueUrl;
        }
        catch (QueueNameExistsException)
        {
            var response = await sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
            queueUrl = response.QueueUrl;
        }

        if (string.IsNullOrEmpty(queueUrl))
        {
            throw new InvalidOperationException($"Could not create Queue queue: {queueName} on {AwsConnection.Region}");
        }

        return queueUrl;
    }

    private async Task<string> CreateDeadLetterQueueAsync(
        SqsAttributes sqsAttributes,
        CancellationToken cancellationToken)
    {
        using var sqsClient = _awsClientFactory.CreateSqsClient();

        var queueName = sqsAttributes.RedrivePolicy!.DeadlLetterQueueName;

        if (sqsAttributes.Type == SqsType.Fifo)
        {
            queueName = queueName.ToValidSQSQueueName(true);
        }

        Dictionary<string, string?> attributes = CreateQueueAttributes(sqsAttributes, true);
        Dictionary<string, string> tags = CreateQueueTags(sqsAttributes);

        string queueUrl;

        try
        {
            var request = new CreateQueueRequest(queueName) { Attributes = attributes, Tags = tags };
            // create queue is idempotent, so safe to call even if queue already exists
            var response = await sqsClient.CreateQueueAsync(request, cancellationToken);

            queueUrl = response.QueueUrl ?? throw new InvalidOperationException(
                $"Could not find create DLQ, status: {response.HttpStatusCode}");
        }
        catch (QueueNameExistsException)
        {
            var response = await sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
            queueUrl = response.QueueUrl;
        }

        var attributesResponse = await sqsClient.GetQueueAttributesAsync(
            new GetQueueAttributesRequest { QueueUrl = queueUrl, AttributeNames = [QueueAttributeName.QueueArn] },
            cancellationToken);

        if (attributesResponse.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Could not find ARN of DLQ, status: {attributesResponse.HttpStatusCode}");
        }

        return attributesResponse.QueueARN;
    }

    private Dictionary<string, string?> CreateQueueAttributes(SqsAttributes sqsAttributes, bool isDLQ = false)
    {
        var attributes = new Dictionary<string, string?>();

        CreateCommonQueueAttributes(sqsAttributes, isDLQ, attributes);

        if (sqsAttributes.Type != SqsType.Fifo) return attributes;

        CreateFifoQueueAttributes(sqsAttributes, attributes);

        return attributes;
    }

    private static void CreateFifoQueueAttributes(SqsAttributes sqsAttributes, Dictionary<string, string?> attributes)
    {
        attributes.Add(QueueAttributeName.FifoQueue, "true");
        if (sqsAttributes.ContentBasedDeduplication)
        {
            attributes.Add(QueueAttributeName.ContentBasedDeduplication, "true");
        }

        if (sqsAttributes.DeduplicationScope == null || sqsAttributes.FifoThroughputLimit == null)
            return ;
     
        attributes.Add(QueueAttributeName.FifoThroughputLimit, Convert.ToString(sqsAttributes.FifoThroughputLimit.Value.AsString()));
        attributes.Add(QueueAttributeName.DeduplicationScope, sqsAttributes.DeduplicationScope switch
        {
            DeduplicationScope.MessageGroup => "messageGroup",
            _ => "queue"
        });
    }

    private void CreateCommonQueueAttributes(SqsAttributes sqsAttributes, bool isDLQ, Dictionary<string, string?> attributes)
    {
        if (!isDLQ && sqsAttributes.RedrivePolicy != null)
        {
            var policy = new
            {
                maxReceiveCount = sqsAttributes.RedrivePolicy.MaxReceiveCount, 
                deadLetterTargetArn = ChannelDeadLetterQueueArn
            };

            attributes.Add(QueueAttributeName.RedrivePolicy, JsonSerializer.Serialize(policy, JsonSerialisationOptions.Options));
        }

        attributes.Add(QueueAttributeName.DelaySeconds, Convert.ToString(Convert.ToInt32(sqsAttributes.DelaySeconds.TotalSeconds)));
        attributes.Add(QueueAttributeName.MessageRetentionPeriod, Convert.ToString(Convert.ToInt32(sqsAttributes.MessageRetentionPeriod.TotalSeconds)));
        if (sqsAttributes.TimeOut != null)
            attributes.Add(QueueAttributeName.ReceiveMessageWaitTimeSeconds,
                Convert.ToString(Convert.ToInt32(sqsAttributes.TimeOut.Value.TotalSeconds), CultureInfo.InvariantCulture));
        attributes.Add(QueueAttributeName.VisibilityTimeout, Convert.ToString(Convert.ToInt32(sqsAttributes.LockTimeout.TotalSeconds), CultureInfo.InvariantCulture));

        if (sqsAttributes.IamPolicy != null)
        {
            attributes.Add(QueueAttributeName.Policy, sqsAttributes.IamPolicy);
        }
    }

    private Dictionary<string, string> CreateQueueTags(SqsAttributes? sqsAttributes)
    {
        var tags = new Dictionary<string, string> { { "Source", "Brighter" } };
        if (sqsAttributes?.Tags == null) return tags;
        
        foreach (var tag in sqsAttributes.Tags)
        {
            tags.Add(tag.Key, tag.Value);
        }

        return tags;
    }
    
    private static Dictionary<string, string?> CreateTopicAttributes(SnsAttributes snsAttributes)
    {
        var attributes = new Dictionary<string, string?>();
        if (!string.IsNullOrEmpty(snsAttributes.DeliveryPolicy))
            attributes.Add("DeliveryPolicy", snsAttributes.DeliveryPolicy);

        if (!string.IsNullOrEmpty(snsAttributes.Policy))
            attributes.Add("Policy", snsAttributes.Policy);

        if (snsAttributes.Type != SqsType.Fifo) return attributes;
        
        attributes.Add("FifoTopic", "true");
        if (snsAttributes.ContentBasedDeduplication)
        {
            attributes.Add("ContentBasedDeduplication", "true");
        }

        return attributes;
    }

    private static async Task<string?> GetQueueArnForChannelAsync(string queueUrl, AmazonSQSClient sqsClient)
    {
        var result = await sqsClient.GetQueueAttributesAsync(
            new GetQueueAttributesRequest { QueueUrl = queueUrl, AttributeNames = [QueueAttributeName.QueueArn] }
        );

        if (result.HttpStatusCode == HttpStatusCode.OK)
        {
            return result.QueueARN;
        }

        return null;
    }

    private IValidateTopic GetTopicValidationStrategy(TopicFindBy findTopicBy, SqsType type)
        => findTopicBy switch
        {
            TopicFindBy.Arn => new ValidateTopicByArn(_awsClientFactory.CreateSnsClient()),
            TopicFindBy.Name => new ValidateTopicByName(_awsClientFactory.CreateSnsClient(), type),
            TopicFindBy.Convention => new ValidateTopicByArnConvention(AwsConnection.Credentials,
                AwsConnection.Region,
                AwsConnection.ClientConfigAction,
                type),
            _ => throw new ConfigurationException("Unknown TopicFindBy used to determine how to read RoutingKey")
        };

    private async Task SubscribeToTopicAsync(string topicArn,
        string queueUrl,
        SqsAttributes? sqsAttributes,
        AmazonSQSClient sqsClient,
        AmazonSimpleNotificationServiceClient snsClient, CancellationToken cancellationToken)
    {
        var arn = await snsClient.SubscribeQueueAsync(topicArn, sqsClient, queueUrl);
        if (string.IsNullOrEmpty(arn))
        {
            throw new InvalidOperationException(
                $"Could not subscribe to topic: {topicArn} from queue: {queueUrl} in region {AwsConnection.Region}");
        }

        var response = await snsClient.SetSubscriptionAttributesAsync(
            new SetSubscriptionAttributesRequest(arn,
                "RawMessageDelivery",
                sqsAttributes?.RawMessageDelivery.ToString()), cancellationToken);

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("Unable to set subscription attribute for raw message delivery");
        }
    }

    private static async Task<bool> SubscriptionExistsAsync(string topicArn,
        string queueUrl,
        AmazonSQSClient sqsClient,
        AmazonSimpleNotificationServiceClient snsClient, CancellationToken cancellationToken)
    {
        var queueArn = await GetQueueArnForChannelAsync(queueUrl, sqsClient);

        if (queueArn == null)
        {
            throw new BrokerUnreachableException($"Could not find queue ARN for queue {queueUrl}");
        }

        bool exists;
        ListSubscriptionsByTopicResponse response;
        do
        {
            response = await snsClient.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest { TopicArn = topicArn }, cancellationToken);
            exists = response.Subscriptions != null && response.Subscriptions.Any(sub => "sqs".Equals(sub.Protocol, StringComparison.OrdinalIgnoreCase) && sub.Endpoint == queueArn);
        } while (!exists && response.NextToken != null);

        return exists;
    }

    private async Task<string?> ValidateQueueAsync(
        string queueName,
        QueueFindBy findBy,
        SqsType type,
        OnMissingChannel makeChannel,
        CancellationToken cancellationToken)
    {
        var validationStrategy = GetQueueValidationStrategy(findBy, type);
        var (exists, queueUrl) = await validationStrategy.ValidateAsync(queueName, cancellationToken);

        if (exists)
            return queueUrl;

        throw new QueueDoesNotExistException(
            $"Queue validation error: could not find queue {queueName}. Did you want Brighter to create infrastructure?");
    }

    private IValidateQueue GetQueueValidationStrategy(QueueFindBy findQueueBy, SqsType type)
        => findQueueBy switch
        {
            QueueFindBy.Url => new ValidateQueueByUrl(_awsClientFactory.CreateSqsClient()),
            QueueFindBy.Name => new ValidateQueueByName(_awsClientFactory.CreateSqsClient(), type),
            _ => throw new ConfigurationException("Unknown TopicFindBy used to determine how to read RoutingKey")
        };

    private async Task<string?> ValidateTopicAsync(RoutingKey topic, TopicFindBy findTopicBy, SqsType sqsType,
        CancellationToken cancellationToken = default)
    {
        var topicValidationStrategy = GetTopicValidationStrategy(findTopicBy, sqsType);
        var (exists, topicArn) = await topicValidationStrategy.ValidateAsync(topic, cancellationToken);

        if (exists)
        {
            return topicArn;
        }

        throw new BrokerUnreachableException(
            $"Topic validation error: could not find topic {topic}. Did you want Brighter to create infrastructure?");
    }
}
