namespace Paramore.Brighter.Locking.PostgresSql;

public class PostgresLockingProviderOptions(string connectionString)
{
    public string ConnectionString { get; } = connectionString;
}
