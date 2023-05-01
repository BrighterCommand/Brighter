using FluentMigrator;

namespace Greetings_SqliteMigrations.Migrations;

[Migration(1)]
public class SqlliteInitialCreate : Migration
{
    public override void Up()
    {
        Create.Table("Person")
            .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn("Name").AsString().Unique()
            .WithColumn("TimeStamp").AsBinary().WithDefault(SystemMethods.CurrentDateTime);

        Create.Table("Greeting")
            .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn("Message").AsString()
            .WithColumn("Recipient_Id").AsInt32();

        Create.ForeignKey()
            .FromTable("Greeting").ForeignColumn("Recipient_Id")
            .ToTable("Person").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.Table("Greeting");
        Delete.Table("Person");
    }
}
