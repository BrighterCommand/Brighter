using System.Data.Common;
using Microsoft.Data.SqlClient;
using Paramore.Brighter;

namespace Orders.Data;

public class SqlConnectionProvider : RelationalDbConnectionProvider
{
    private readonly SqlUnitOfWork _sqlConnection;
    
    public SqlConnectionProvider(SqlUnitOfWork sqlConnection)
    {
        _sqlConnection = sqlConnection;
    }
    
    public override DbConnection GetConnection()
    {
        return _sqlConnection.Connection;
    }

    public override SqlTransaction? GetTransaction()
    {
        return _sqlConnection.Transaction;
    }

    public override bool HasOpenTransaction { get => _sqlConnection.Transaction != null; }
    public override bool IsSharedConnection { get => true; }
}
