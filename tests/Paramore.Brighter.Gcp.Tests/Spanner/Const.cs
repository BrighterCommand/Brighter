using Paramore.Brighter.Gcp.Tests.Helper;

namespace Paramore.Brighter.Gcp.Tests.Spanner;

public static class Const
{
    public static string ConnectionString => $"Data Source=projects/{GatewayFactory.GetProjectId()}/instances/brighter-spanner/databases/brightertests";
    public const string TablePrefix = "test_";
}
