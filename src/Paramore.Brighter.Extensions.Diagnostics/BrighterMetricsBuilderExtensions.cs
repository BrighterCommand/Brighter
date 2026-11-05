#region Licence

/* The MIT License (MIT)
Copyright © 2025 Tim Salva <tim@jtsalva.dev>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using Paramore.Brighter.CircuitBreaker;
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
            services.TryAddSingleton<IAmABrighterSweeperMeter>(serviceProvider => new SweeperMeter(
                serviceProvider.GetRequiredService<IMeterFactory>(),
                serviceProvider.GetService<OutboxSweeperState>(),
                () => serviceProvider.GetService<IAmAnOutboxCircuitBreaker>()));
            services.TryAddSingleton<PublishConfirmationMeter>();
            services.TryAddSingleton<IAmABrighterOutboxMeter, OutboxMeter>();
            services.TryAddSingleton<OutboxStaleMessageMeter>();
            
            builder.AddMeter(BrighterSemanticConventions.MeterName);
        });

        if (builder is IDeferredMeterProviderBuilder deferred)
            deferred.Configure((serviceProvider, _) => CreateMetersNothingDependsOn(serviceProvider));
        
        return builder;
    }

    private static void CreateMetersNothingDependsOn(IServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<PublishConfirmationMeter>();
        serviceProvider.GetRequiredService<IAmABrighterSweeperMeter>();
        serviceProvider.GetRequiredService<OutboxStaleMessageMeter>();
    }
}