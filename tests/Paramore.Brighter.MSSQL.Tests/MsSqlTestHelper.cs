using System;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MSSQL.Tests
{
    public class MsSqlTestHelper
    {
        private readonly bool _binaryMessagePayload;
        private string _tableName;
        private readonly SqlSettings _sqlSettings;
        private readonly IAmARelationalDbConnectionProvider _connectionProvider;

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
            new(_sqlSettings.TestsBrighterConnectionString, inboxTableName: _tableName, binaryMessagePayload: _binaryMessagePayload);

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

            _connectionProvider = new MsSqlConnectionProvider(new RelationalDatabaseConfiguration(_sqlSettings.TestsBrighterConnectionString));
            
            EnsureDatabaseExists(_sqlSettings.TestsBrighterConnectionString);
        }

        public void SetupQueueDb()
        {
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

        private static void EnsureDatabaseExists(string connectionString)
        {
            Configuration.EnsureDatabaseExists(connectionString);
        }
    }

    internal sealed class SqlSettings
    {
        public string TestsBrighterConnectionString { get; set; } =
            "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password123!;Application Name=BrighterTests;Connect Timeout=60;Encrypt=false";
    }
}
