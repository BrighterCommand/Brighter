using System;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests
{
    public class MsSqlTestHelper
    {
        private string _tableName;
        private SqlSettings _sqlSettings;

        private const string _queueDDL = @"CREATE TABLE [dbo].[{0}](
                [Id][bigint] IDENTITY(1, 1) NOT NULL,
                [Topic] [nvarchar](255) NOT NULL,
                [MessageType] [nvarchar](1024) NOT NULL,
                [Payload] [nvarchar](max)NOT NULL,
                CONSTRAINT[PK_QueueData_{1}] PRIMARY KEY CLUSTERED
                    (
                [Id] ASC
                )WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON[PRIMARY]
                ) ON[PRIMARY] TEXTIMAGE_ON[PRIMARY]";

        public MsSqlTestHelper()
        {
            var builder = new ConfigurationBuilder().AddEnvironmentVariables();
            var configuration = builder.Build();

            _sqlSettings = new SqlSettings();
            configuration.GetSection("Sql").Bind(_sqlSettings);

            _tableName = $"test_{Guid.NewGuid()}";

        }

       public void CreateDatabase()
        {
            using (var connection = new SqlConnection(_sqlSettings.TestsMasterConnectionString))
            {
                connection.Open();
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

        public MsSqlInboxConfiguration InboxConfiguration => new MsSqlInboxConfiguration(_sqlSettings.TestsBrighterConnectionString, _tableName);

        public MsSqlOutboxConfiguration OutboxConfiguration => new MsSqlOutboxConfiguration(_sqlSettings.TestsBrighterConnectionString, _tableName);

        public MsSqlMessagingGatewayConfiguration QueueConfiguration => new MsSqlMessagingGatewayConfiguration(_sqlSettings.TestsBrighterConnectionString, _tableName);
        
        private void CreateQueueTable()
        {
            _tableName = $"queue_{_tableName}";
            using var connection = new SqlConnection(_sqlSettings.TestsBrighterConnectionString);
            var createTableSql = string.Format(_queueDDL, _tableName, Guid.NewGuid().ToString());

            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = createTableSql;
                command.ExecuteNonQuery();
            }
            connection.Close();
        }
        public void CleanUpDb()
        {
            using (var connection = new SqlConnection(_sqlSettings.TestsBrighterConnectionString))
            {
                connection.Open();
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
            using (var connection = new SqlConnection(_sqlSettings.TestsBrighterConnectionString))
            {
                _tableName = $"[message_{_tableName}]";
                var createTableSql = SqlOutboxBuilder.GetDDL(_tableName);

                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateInboxTable()
        {
            using (var connection = new SqlConnection(_sqlSettings.TestsBrighterConnectionString))
            {
                _tableName = $"[command_{_tableName}]";
                var createTableSql = SqlInboxBuilder.GetDDL(_tableName);

                connection.Open();
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
            "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password1!;Application Name=BrighterTests";

        public string TestsMasterConnectionString { get; set; } =
            "Server=127.0.0.1,11433;Database=master;User Id=sa;Password=Password1!;Application Name=BrighterTests";
    }
}
