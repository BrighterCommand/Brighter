namespace GreetingsMigrations.Migrations;

public interface IAmAMigrationConfiguration
{
    string DbType { get; set; }
}

public class MigrationConfiguration : IAmAMigrationConfiguration
{
    public string DbType { get; set; }
}
