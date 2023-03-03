namespace Orders.Sweeper.Extensions;

public static class FlagstoneAppBuilderExtensions
{
    public const string HealthCheckPath = "/health";

    public static bool IsHealthCheck(this HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        return path.Equals(HealthCheckPath, StringComparison.OrdinalIgnoreCase);
    }
}
