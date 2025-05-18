﻿using FluentMigrator;

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
        var timestampColumn = _configuration.DbType == "Postgres" ? "timeStamp" : "TimeStamp";
        
        var person = Create.Table("Person")
            .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn("Name").AsString().Unique()
            .WithColumn(timestampColumn).AsDateTime().Nullable().WithDefault(SystemMethods.CurrentDateTime);
        
        var greeting = Create.Table("Greeting")
            .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
            .WithColumn("Message").AsString()
            .WithColumn("RecipientId").AsInt32();

        if (_configuration.DbType != "Sqlite")
        {
            Create.ForeignKey()
                .FromTable("Greeting").ForeignColumn("RecipientId")
                .ToTable("Person").PrimaryColumn("Id");
        }
    }

    public override void Down()
    {
        Delete.Table("Greeting");
        Delete.Table("Person");
    }    
}
