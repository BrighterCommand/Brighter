using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Extensions.Diagnostics;

public static class BrighterTracerBuilderExtensions
{
    public static TracerProviderBuilder AddBrighterInstrumentation(this TracerProviderBuilder builder)
        => builder.AddSource("Paramore.Brighter");
}
