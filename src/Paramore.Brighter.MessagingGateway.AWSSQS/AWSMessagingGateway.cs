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
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

public class AWSMessagingGateway(AWSMessagingGatewayConnection awsConnection)
{
    protected static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AWSMessagingGateway>();
        
    private readonly AWSClientFactory _awsClientFactory = new(awsConnection);
    protected readonly AWSMessagingGatewayConnection AwsConnection = awsConnection;
    protected string? ChannelTopicArn;

    protected async Task<string?> EnsureTopicAsync(
        RoutingKey topic, 
        TopicFindBy topicFindBy, 
        SnsAttributes? attributes, 
        OnMissingChannel makeTopic = OnMissingChannel.Create, 
        CancellationToken cancellationToken = default)
    {
        //on validate or assume, turn a routing key into a topicARN
        if ((makeTopic == OnMissingChannel.Assume) || (makeTopic == OnMissingChannel.Validate)) 
            await ValidateTopicAsync(topic, topicFindBy, cancellationToken);
        else if (makeTopic == OnMissingChannel.Create) await CreateTopicAsync(topic, attributes);
        return ChannelTopicArn;
    }

    private async Task CreateTopicAsync(RoutingKey topicName, SnsAttributes? snsAttributes)
    {
        using var snsClient = _awsClientFactory.CreateSnsClient();
        var attributes = new Dictionary<string, string?>();
        if (snsAttributes != null)
        {
            if (!string.IsNullOrEmpty(snsAttributes.DeliveryPolicy)) attributes.Add("DeliveryPolicy", snsAttributes.DeliveryPolicy);
            if (!string.IsNullOrEmpty(snsAttributes.Policy)) attributes.Add("Policy", snsAttributes.Policy);
        }

        var createTopicRequest = new CreateTopicRequest(topicName)
        {
            Attributes = attributes,
            Tags = new List<Tag> {new Tag {Key = "Source", Value = "Brighter"}}
        };
                
        //create topic is idempotent, so safe to call even if topic already exists
        var createTopic = await snsClient.CreateTopicAsync(createTopicRequest);
                
        if (!string.IsNullOrEmpty(createTopic.TopicArn))
            ChannelTopicArn = createTopic.TopicArn;
        else
            throw new InvalidOperationException($"Could not create Topic topic: {topicName} on {AwsConnection.Region}");
    }

    private async Task ValidateTopicAsync(RoutingKey topic, TopicFindBy findTopicBy, CancellationToken cancellationToken = default)
    {
        IValidateTopic topicValidationStrategy = GetTopicValidationStrategy(findTopicBy);
        (bool exists, string? topicArn) = await topicValidationStrategy.ValidateAsync(topic);
        if (exists)
            ChannelTopicArn = topicArn;
        else
            throw new BrokerUnreachableException(
                $"Topic validation error: could not find topic {topic}. Did you want Brighter to create infrastructure?");
    }

    private IValidateTopic GetTopicValidationStrategy(TopicFindBy findTopicBy)
    {
        switch (findTopicBy)
        {
            case TopicFindBy.Arn:
                return new ValidateTopicByArn(AwsConnection.Credentials, AwsConnection.Region, AwsConnection.ClientConfigAction);
            case TopicFindBy.Convention:
                return new ValidateTopicByArnConvention(AwsConnection.Credentials, AwsConnection.Region, AwsConnection.ClientConfigAction);
            case TopicFindBy.Name:
                return new ValidateTopicByName(AwsConnection.Credentials, AwsConnection.Region, AwsConnection.ClientConfigAction);
            default:
                throw new ConfigurationException("Unknown TopicFindBy used to determine how to read RoutingKey");
        }
    }
}
