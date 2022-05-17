using FluentMigrator;

namespace Salutations_SqliteMigrations.Migrations;

public class SqliteInitialCreate : Migration 
{
    public override void Up()
    {
        Create.Table("Salutation")
            .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn("Greeting").AsString()
            .WithColumn("TimeStamp").AsBinary().WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        Delete.Table("Salutation");
    }
}
