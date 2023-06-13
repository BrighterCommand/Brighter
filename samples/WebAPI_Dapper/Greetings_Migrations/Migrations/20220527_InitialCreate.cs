using FluentMigrator;

namespace Greetings_MySqlMigrations.Migrations;

[Migration(1)]
public class SqlInitialCreate : Migration 
{
    private readonly IAmAMigrationConfiguration _configuration;

    public SqlInitialCreate(IAmAMigrationConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public override void Up()
    {
        var personTableName = _configuration.DbType == "Postgres" ? "person" : "Person";
        var idColumn = _configuration.DbType == "Postgres" ? "id": "Id";
        var nameColumn = _configuration.DbType == "Postgres" ? "name" : "Name";
        var timestampColumn = _configuration.DbType == "Postgres" ? "timeStamp" : "TimeStamp";
        var person = Create.Table(personTableName)
            .WithColumn(idColumn).AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn(nameColumn).AsString().Unique()
            .WithColumn(timestampColumn).AsDateTime().Nullable().WithDefault(SystemMethods.CurrentDateTime);
        
        var greetingTableName = _configuration.DbType == "Postgres" ? "greeting" : "Greeting";
        var recipientIdColumn = _configuration.DbType == "Postgres" ? "recipient_id": "Recipient_Id";
        var messageColumn = _configuration.DbType == "Postgres" ? "message": "Message";
        var greeting = Create.Table(greetingTableName)
            .WithColumn(idColumn).AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn(messageColumn).AsString()
            .WithColumn(recipientIdColumn).AsInt32();

        Create.ForeignKey()
            .FromTable(greetingTableName).ForeignColumn(recipientIdColumn)
            .ToTable(personTableName).PrimaryColumn(idColumn);
    }

    public override void Down()
    {
        Delete.Table("Greeting");
        Delete.Table("Person");
    }    
}
