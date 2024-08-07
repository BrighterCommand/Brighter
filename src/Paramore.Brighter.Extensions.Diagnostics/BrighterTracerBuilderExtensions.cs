using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Extensions.Diagnostics;

public static class BrighterTracerBuilderExtensions
{
    public static TracerProviderBuilder AddBrighterInstrumentation(this TracerProviderBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var brighterTracer = new BrighterTracer(TimeProvider.System);
            services.TryAddSingleton<IAmABrighterTracer>(brighterTracer);

        
            builder.AddSource(brighterTracer.ActivitySource.Name);
        });
        
        return builder;
    }

}
