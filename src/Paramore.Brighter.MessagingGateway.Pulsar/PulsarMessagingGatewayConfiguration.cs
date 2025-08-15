using System;

namespace Paramore.Brighter.MessagingGateway.Pulsar
{
    public class PulsarMessagingGatewayConfiguration
    {
        public string ServiceUrl { get; set; } = "pulsar://localhost:6650";
        public string Topic { get; set; } = "";
        public string SubscriptionName { get; set; } = "";

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ServiceUrl))
                throw new ArgumentException("ServiceUrl is required", nameof(ServiceUrl));
            if (string.IsNullOrWhiteSpace(Topic))
                throw new ArgumentException("Topic is required", nameof(Topic));
            if (string.IsNullOrWhiteSpace(SubscriptionName))
                throw new ArgumentException("SubscriptionName is required", nameof(SubscriptionName));
        }
    }
}
