﻿using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests
{
    public class MsSqlTestHelper
    {
        private readonly bool _binaryMessagePayload;
        private string _tableName;
        private SqlSettings _sqlSettings;
        private IAmARelationalDbConnectionProvider _connectionProvider;
        private IAmARelationalDbConnectionProvider _masterConnectionProvider;

        private const string _textQueueDDL = @"CREATE TABLE [dbo].[{0}](
                [Id][bigint] IDENTITY(1, 1) NOT NULL,
                [Topic] [nvarchar](255) NOT NULL,
                [MessageType] [nvarchar](1024) NOT NULL,
                [Payload] [nvarchar](max)NOT NULL,
                CONSTRAINT[PK_QueueData_{1}] PRIMARY KEY CLUSTERED
                    (
                [Id] ASC
                )WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY]
                ) ON[PRIMARY] TEXTIMAGE_ON[PRIMARY]";
        
        private const string _binaryQueueDDL = @"CREATE TABLE [dbo].[{0}](
                [Id][bigint] IDENTITY(1, 1) NOT NULL,
                [Topic] [nvarchar](255) NOT NULL,
                [MessageType] [nvarchar](1024) NOT NULL,
                [Payload] [varbinary](max)NOT NULL,
                CONSTRAINT[PK_QueueData_{1}] PRIMARY KEY CLUSTERED
                    (
                [Id] ASC
                )WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY]
                ) ON[PRIMARY] TEXTIMAGE_ON[PRIMARY]";
        
        public RelationalDatabaseConfiguration InboxConfiguration => 
            new(_sqlSettings.TestsBrighterConnectionString, inboxTableName: _tableName);

        public RelationalDatabaseConfiguration OutboxConfiguration => 
            new(_sqlSettings.TestsBrighterConnectionString, outBoxTableName: _tableName, binaryMessagePayload: _binaryMessagePayload);

        public RelationalDatabaseConfiguration QueueConfiguration =>
            new(_sqlSettings.TestsBrighterConnectionString, queueStoreTable: _tableName);


        public MsSqlTestHelper(bool binaryMessagePayload = false)
        {
            _binaryMessagePayload = binaryMessagePayload;
            var builder = new ConfigurationBuilder().AddEnvironmentVariables();
            var configuration = builder.Build();

            _sqlSettings = new SqlSettings();
            configuration.GetSection("Sql").Bind(_sqlSettings);

            _tableName = $"test_{Guid.NewGuid()}";

            _connectionProvider =
                new MsSqlConnectionProvider(new RelationalDatabaseConfiguration(_sqlSettings.TestsBrighterConnectionString));
            _masterConnectionProvider =
                new MsSqlConnectionProvider(new RelationalDatabaseConfiguration(_sqlSettings.TestsMasterConnectionString));
        }

        public void CreateDatabase()
        {
            using (var connection = _masterConnectionProvider.GetConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                                        IF DB_ID('BrighterTests') IS NULL
                                        BEGIN
                                            CREATE DATABASE BrighterTests;
                                        END;";
                    command.ExecuteNonQuery();
                }
            }
        }

        public void SetupMessageDb()
        {
            CreateDatabase();
            CreateOutboxTable();
        }

        public void SetupCommandDb()
        {
            CreateDatabase();
            CreateInboxTable();
        }

        public void SetupQueueDb()
        {
            CreateDatabase();
            CreateQueueTable();
        }

        private void CreateQueueTable()
        {
            _tableName = $"queue_{_tableName}";
            using var connection = _connectionProvider.GetConnection();
            var ddl = _binaryMessagePayload ? _binaryQueueDDL : _textQueueDDL;
            var createTableSql = string.Format(ddl, _tableName, Guid.NewGuid().ToString());

            using (var command = connection.CreateCommand())
            {
                command.CommandText = createTableSql;
                command.ExecuteNonQuery();
            }

            connection.Close();
        }

        public void CleanUpDb()
        {
            using (var connection = _connectionProvider.GetConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
                                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{_tableName}') AND type in (N'U'))
                                        BEGIN
                                            DROP TABLE {_tableName}
                                        END;";
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateOutboxTable()
        {
            using (var connection = _connectionProvider.GetConnection())
            {
                _tableName = $"[message_{_tableName}]";
                var createTableSql = SqlOutboxBuilder.GetDDL(_tableName, _binaryMessagePayload);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateInboxTable()
        {
            using (var connection = _connectionProvider.GetConnection())
            {
                _tableName = $"[command_{_tableName}]";
                var createTableSql = SqlInboxBuilder.GetDDL(_tableName);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    command.ExecuteNonQuery();
                }
            }
        }
    }

    internal class SqlSettings
    {
        public string TestsBrighterConnectionString { get; set; } =
            "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password123!;Application Name=BrighterTests;Connect Timeout=60;Encrypt=false";

        public string TestsMasterConnectionString { get; set; } =
            "Server=127.0.0.1,11433;Database=master;User Id=sa;Password=Password123!;Application Name=BrighterTests;Connect Timeout=60;Encrypt=false";
    }
}
