using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Extensions.Diagnostics;

public static class BrighterMetricsBuilderExtensions
{
    public static MeterProviderBuilder AddBrighterInstrumentation(this MeterProviderBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.TryAddSingleton<IAmABrighterMessagingMeter, MessagingMeter>();
            services.TryAddSingleton<IAmABrighterDbMeter, DbMeter>();
            
            builder.AddMeter(BrighterSemanticConventions.MeterName);
        });
        
        return builder;
    }
}