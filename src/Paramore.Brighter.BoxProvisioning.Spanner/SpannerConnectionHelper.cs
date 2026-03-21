using Google.Api.Gax;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Creates Spanner connections with emulator detection enabled, allowing the
/// provisioner to work transparently against both real Spanner and the emulator.
/// </summary>
internal static class SpannerConnectionHelper
{
    internal static SpannerConnection CreateConnection(string connectionString)
    {
        var builder = new SpannerConnectionStringBuilder(connectionString)
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        };
        return new SpannerConnection(builder);
    }
}
