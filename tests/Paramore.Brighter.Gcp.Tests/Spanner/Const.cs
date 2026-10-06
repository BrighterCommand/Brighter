using Google.Api.Gax;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Gcp.Tests.Helper;

namespace Paramore.Brighter.Gcp.Tests.Spanner;

public static class Const
{
    // Embed EmulatorDetection into the connection string so it round-trips through
    // RelationalDatabaseConfiguration into the tests' own DDL connections and the outbox/inbox's
    // internal connections, routing to SPANNER_EMULATOR_HOST when set instead of demanding ADC.
    public static string ConnectionString => new SpannerConnectionStringBuilder(
        $"Data Source=projects/{GatewayFactory.GetProjectId()}/instances/brighter-spanner/databases/brightertests")
    {
        EmulatorDetection = EmulatorDetection.EmulatorOrProduction
    }.ConnectionString;

    public const string TablePrefix = "test_";
}
