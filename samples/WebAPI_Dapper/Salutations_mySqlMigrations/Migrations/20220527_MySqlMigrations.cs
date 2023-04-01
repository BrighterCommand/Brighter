using FluentMigrator;

namespace Salutations_mySqlMigrations.Migrations;

[Migration(1)]
public class MySqlInitialCreate : Migration
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
