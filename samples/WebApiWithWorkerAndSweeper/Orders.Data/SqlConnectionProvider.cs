using Microsoft.Data.SqlClient;
using Orders.Domain;
using Paramore.Brighter.MsSql;

namespace Orders.Data;

public class SqlConnectionProvider : IMsSqlTransactionConnectionProvider
{
    private readonly SqlUnitOfWork _sqlConnection;
    
    public SqlConnectionProvider(SqlUnitOfWork sqlConnection)
    {
        _sqlConnection = sqlConnection;
    }
    
    public SqlConnection GetConnection()
    {
        return _sqlConnection.Connection;
    }

    public Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        return Task.FromResult(_sqlConnection.Connection);
    }

    public SqlTransaction? GetTransaction()
    {
        return _sqlConnection.Transaction;
    }

    public bool HasOpenTransaction { get => _sqlConnection.Transaction != null; }
    public bool IsSharedConnection { get => true; }
}
