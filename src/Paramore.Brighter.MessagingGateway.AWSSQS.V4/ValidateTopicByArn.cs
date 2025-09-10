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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

public class ValidateTopicByArn : IDisposable, IValidateTopic
{
    private AmazonSimpleNotificationServiceClient _snsClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateTopicByArn"/> class.
    /// </summary>
    /// <param name="credentials">The AWS credentials.</param>
    /// <param name="region">The AWS region.</param>
    /// <param name="clientConfigAction">An optional action to configure the client.</param>
    public ValidateTopicByArn(AWSCredentials credentials, RegionEndpoint region, Action<ClientConfig>? clientConfigAction = null)
    {
        var clientFactory = new AWSClientFactory(credentials, region, clientConfigAction);
        _snsClient = clientFactory.CreateSnsClient();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateTopicByArn"/> class.
    /// </summary>
    /// <param name="snsClient">The SNS client.</param>
    public ValidateTopicByArn(AmazonSimpleNotificationServiceClient snsClient)
    {
        _snsClient = snsClient;
    }

    /// <summary>
    /// Validates the specified topic ARN asynchronously.
    /// </summary>
    /// <param name="topicArn">The ARN of the topic to validate.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A tuple indicating whether the topic is valid and its ARN.</returns>
    public virtual async Task<(bool, string? TopicArn)> ValidateAsync(string topicArn, CancellationToken cancellationToken = default)
    {
        bool exists = false;

        try
        {
            var topicAttributes = await _snsClient.GetTopicAttributesAsync(
                new GetTopicAttributesRequest(topicArn), cancellationToken);

            exists = ((topicAttributes.HttpStatusCode == HttpStatusCode.OK)  && (topicAttributes.Attributes["TopicArn"] == topicArn));
        }
        catch (InternalErrorException)
        {
            exists = false;
        }
        catch (NotFoundException)
        {
            exists = false;
        }
        catch (AuthorizationErrorException)
        {
            exists = false;
        }

        return (exists, topicArn);
    }

    /// <summary>
    /// Disposes the SNS client.
    /// </summary>
    public void Dispose()
    {
        _snsClient?.Dispose();
    }
}