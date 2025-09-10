using System;
using System.Linq;
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
            
            var hasMessagingMeter = services.Any(sd => sd.ServiceType == typeof(IAmABrighterMessagingMeter));
            var hasDbMeter = services.Any(sd => sd.ServiceType == typeof(IAmABrighterDbMeter));

            if (hasMessagingMeter && hasDbMeter)
                builder.AddProcessor<BrighterMetricsFromTracesProcessor>();
        });
        
        return builder;
    }
    
    /// <summary>
    /// Register a tail-based sampler delegating the sampling decision to any OpenTelemetry Sampler.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// <code>
    /// // Use a TraceIdRatioBasedSampler with 25% sampling rate
    /// builder.SetTailSampler(new TraceIdRatioBasedSampler(0.25));
    /// 
    /// // Use default constructor of AlwaysOnSampler
    /// builder.SetTailSampler&lt;AlwaysOnSampler&gt;();
    /// </code>
    /// </remarks>
    /// <param name="builder">The TracerProviderBuilder to configure</param>
    /// <param name="sampler">Optional sampler instance. If null, will create a new instance using the default constructor</param>
    /// <typeparam name="TSampler">Type of sampler to use</typeparam>
    /// <returns>The TracerProviderBuilder for chaining</returns>
    public static TracerProviderBuilder SetTailSampler<TSampler>(this TracerProviderBuilder builder, TSampler? sampler = null)
        where TSampler : Sampler, new()
    {
        builder.AddProcessor(new TailSamplerProcessor(sampler ?? new TSampler()));
        return builder;
    }
}
