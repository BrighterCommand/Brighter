using System;
using DotPulsar;
using DotPulsar.Abstractions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

public class PulsarMessagingGatewayConnection
{
    public string? ProducerName { get; set; }
    public Uri? ServiceUrl { get; set; }
    
    public Action<IPulsarClientBuilder>? Configuration { get; set; }

    public InstrumentationOptions Instrumentation { get; set; } = InstrumentationOptions.All;
    
    
    private IPulsarClient? _pulsarClient;
    public IPulsarClient Create()
    {
        if (_pulsarClient != null)
        {
            return _pulsarClient;
        }
        
        var builder = PulsarClient.Builder();

        if (ServiceUrl != null)
        {
            builder.ServiceUrl(ServiceUrl);
        }
        
        Configuration?.Invoke(builder);
        
        return _pulsarClient = builder.Build();
    }
}
