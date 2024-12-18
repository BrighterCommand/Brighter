using System;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    internal class AWSClientFactory
    {
        private readonly AWSCredentials _credentials;
        private readonly RegionEndpoint _region;
        private readonly Action<ClientConfig>? _clientConfigAction;

        public AWSClientFactory(AWSMessagingGatewayConnection connection)
        {
            _credentials = connection.Credentials;
            _region = connection.Region;
            _clientConfigAction = connection.ClientConfigAction;
        }

        public AWSClientFactory(AWSCredentials credentials, RegionEndpoint region, Action<ClientConfig>? clientConfigAction)
        {
            _credentials = credentials;
            _region = region;
            _clientConfigAction = clientConfigAction;
        }

        public AmazonSimpleNotificationServiceClient CreateSnsClient()
        {
            var config = new AmazonSimpleNotificationServiceConfig
            {
                RegionEndpoint = _region
            };

            if (_clientConfigAction != null)
            {
                _clientConfigAction(config);
            }

            return new AmazonSimpleNotificationServiceClient(_credentials, config);
        }

        public AmazonSQSClient CreateSqsClient()
        {
            var config = new AmazonSQSConfig
            {
                RegionEndpoint = _region
            };

            if (_clientConfigAction != null)
            {
                _clientConfigAction(config);
            }

            return new AmazonSQSClient(_credentials, config);
        }

        public AmazonSecurityTokenServiceClient CreateStsClient()
        {
            var config = new AmazonSecurityTokenServiceConfig
            {
                RegionEndpoint = _region
            };

            if (_clientConfigAction != null)
            {
                _clientConfigAction(config);
            }

            return new AmazonSecurityTokenServiceClient(_credentials, config);
        }
    }
}
