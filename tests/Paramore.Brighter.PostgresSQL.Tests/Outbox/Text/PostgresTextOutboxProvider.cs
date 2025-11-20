using System.Collections.Generic;
using System.Data.Common;
using Npgsql;
using Paramore.Brighter.Outbox.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox.Text;

public class PostgresTextOutboxProvider : IAmAnOutboxProvider
{
    private readonly RelationalDatabaseConfiguration _configuration = new(Const.ConnectionString, $"Table{Uuid.New():N}");
    
    public void CreateStore()
    {
        using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlOutboxBuilder.GetDDL(_configuration.OutBoxTableName);
        command.ExecuteNonQuery();
    }

    public void DeleteStore(IEnumerable<Message> messages)
    {
        using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }

    public IAmAnOutboxSync<Message, DbTransaction> CreateOutbox()
    {
        return new PostgreSqlOutbox(_configuration);
    }
}
