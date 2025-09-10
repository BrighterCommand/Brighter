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
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// The Aws Client factory
/// </summary>
/// <param name="credentials"></param>
/// <param name="region"></param>
/// <param name="clientConfigAction"></param>
public class AWSClientFactory(
    AWSCredentials credentials,
    RegionEndpoint region,
    Action<ClientConfig>? clientConfigAction)
{
    public AWSCredentials Credentials => credentials;

    public RegionEndpoint RegionEndpoint => region;
    public Action<ClientConfig>? ClientConfigAction => clientConfigAction;

    public AWSClientFactory(AWSMessagingGatewayConnection connection)
        : this(connection.Credentials, connection.Region, connection.ClientConfigAction)
    {
    }

    /// <summary>
    /// Create SNS Client
    /// </summary>
    /// <returns>New instance <see cref="AmazonSimpleNotificationServiceClient"/>.</returns>
    public AmazonSimpleNotificationServiceClient CreateSnsClient()
    {
        var config = new AmazonSimpleNotificationServiceConfig { RegionEndpoint = RegionEndpoint };

        ClientConfigAction?.Invoke(config);

        return new AmazonSimpleNotificationServiceClient(Credentials, config);
    }

    /// <summary>
    /// Create SQS Client
    /// </summary>
    /// <returns>New instance <see cref="AmazonSQSClient"/>.</returns>
    public AmazonSQSClient CreateSqsClient()
    {
        var config = new AmazonSQSConfig { RegionEndpoint = RegionEndpoint };

        ClientConfigAction?.Invoke(config);

        return new AmazonSQSClient(Credentials, config);
    }

    /// <summary>
    /// Create STS Client
    /// </summary>
    /// <returns>New instance <see cref="AmazonSecurityTokenServiceClient"/>.</returns>
    public AmazonSecurityTokenServiceClient CreateStsClient()
    {
        var config = new AmazonSecurityTokenServiceConfig { RegionEndpoint = RegionEndpoint };

        ClientConfigAction?.Invoke(config);

        return new AmazonSecurityTokenServiceClient(Credentials, config);
    }
}
