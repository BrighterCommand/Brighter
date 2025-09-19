using FluentMigrator;

namespace SalutationsMigrations.Migrations;

[Migration(1)]
public class SqlInitialMigrations : Migration
{
    public override void Up()
    {
        Create.Table("Salutation")
            .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn("Greeting").AsString()
            .WithColumn("TimeStamp").AsDateTime().Nullable().WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        Delete.Table("Salutation");
    }
}
