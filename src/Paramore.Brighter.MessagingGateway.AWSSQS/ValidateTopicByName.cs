﻿#region Licence
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
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// The <see cref="ValidateTopicByName"/> class is responsible for validating an AWS SNS topic by its name.
    /// </summary>
    internal class ValidateTopicByName : IValidateTopic
    {
        private readonly AmazonSimpleNotificationServiceClient _snsClient;
        private readonly SnsSqsType _type;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidateTopicByName"/> class.
        /// </summary>
        /// <param name="credentials">The AWS credentials.</param>
        /// <param name="region">The AWS region.</param>
        /// <param name="clientConfigAction">An optional action to configure the client.</param>
        /// <param name="type">The SNS Type.</param>
        public ValidateTopicByName(AWSCredentials credentials, RegionEndpoint region, Action<ClientConfig>? clientConfigAction = null, SnsSqsType type = SnsSqsType.Standard)
        {
            var clientFactory = new AWSClientFactory(credentials, region, clientConfigAction);
            _snsClient = clientFactory.CreateSnsClient();
            _type = type;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidateTopicByName"/> class.
        /// </summary>
        /// <param name="snsClient">The SNS client.</param>
        /// <param name="type">The SNS Type.</param>
        public ValidateTopicByName(AmazonSimpleNotificationServiceClient snsClient, SnsSqsType type = SnsSqsType.Standard)
        {
            _snsClient = snsClient;
            _type = type;
        }

        /// <summary>
        /// Validates the specified topic name asynchronously.
        /// </summary>
        /// <param name="topicName">The name of the topic to validate.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A tuple indicating whether the topic is valid and its ARN.</returns>
        /// <remarks>
        /// Note that we assume here that topic names are globally unique, if not provide the topic ARN directly in the SNSAttributes of the subscription.
        /// This approach can be rate throttled at scale. AWS limits to 30 ListTopics calls per second, so if you have a lot of clients starting,
        /// you may run into issues.
        /// </remarks>
        public async Task<(bool, string? TopicArn)> ValidateAsync(string topicName, CancellationToken cancellationToken = default)
        {
            if (_type == SnsSqsType.Fifo && !topicName.EndsWith(".fifo"))
            {
                topicName += ".fifo";
            }
            
            var topic = await _snsClient.FindTopicAsync(topicName);
            return (topic != null, topic?.TopicArn);
        }
    }
}
