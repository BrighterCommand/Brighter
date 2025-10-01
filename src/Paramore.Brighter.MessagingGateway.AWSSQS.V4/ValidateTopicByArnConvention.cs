#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// The <see cref="ValidateTopicByArnConvention"/> class is responsible for validating an AWS SNS topic by its ARN convention.
/// </summary>
public class ValidateTopicByArnConvention : ValidateTopicByArn, IValidateTopic
{
    private readonly RegionEndpoint _region;
    private readonly AmazonSecurityTokenServiceClient _stsClient;
    private readonly SqsType _type;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateTopicByArnConvention"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials.</param>
    /// <param name="region">The AWS region.</param>
    /// <param name="clientConfigAction">An optional action to configure the client.</param>
    public ValidateTopicByArnConvention(AWSCredentials credentials, RegionEndpoint region, Action<ClientConfig>? clientConfigAction = null, SqsType type = SqsType.Standard) 
        : base(credentials, region, clientConfigAction)
    {
        _region = region;
        _type = type;

        var clientFactory = new AWSClientFactory(credentials, region, clientConfigAction);
        _stsClient = clientFactory.CreateStsClient();
    }

    /// <summary>
    /// Validates the specified topic asynchronously.
    /// </summary>
    /// <param name="topic">The topic to validate.</param>
    /// <param name="cancellationToken">Cancel the validation</param>
    /// <returns>A tuple indicating whether the topic is valid and its ARN.</returns>
    public override async Task<(bool, string? TopicArn)> ValidateAsync(string topic, CancellationToken cancellationToken = default)
    {
        var topicArn = await GetArnFromTopic(topic);
        return await base.ValidateAsync(topicArn, cancellationToken);
    }

    /// <summary>
    /// Gets the ARN from the topic name.
    /// </summary>
    /// <param name="topicName">The name of the topic.</param>
    /// <returns>The ARN of the topic.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the AWS account identity cannot be found.</exception>
    private async Task<string> GetArnFromTopic(string topicName)
    {
        var callerIdentityResponse = await _stsClient.GetCallerIdentityAsync(
            new GetCallerIdentityRequest()
        );

        if (callerIdentityResponse.HttpStatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Could not find identity of AWS account");

        topicName = topicName.ToValidSNSTopicName(_type == SqsType.Fifo);

        return new Arn
        {
            Partition = _region.PartitionName,
            Service = "sns",
            Region = _region.SystemName,
            AccountId = callerIdentityResponse.Account,
            Resource = topicName
        }.ToString();
    }
}
