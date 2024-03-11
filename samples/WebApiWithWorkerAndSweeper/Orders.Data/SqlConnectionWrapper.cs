using System.Data;
using Microsoft.Data.SqlClient;
using Orders.Domain;

namespace Orders.Data;

public class SqlUnitOfWork : IUnitOfWork
{
    public SqlConnection Connection { get; }
    public SqlTransaction? Transaction { get; private set; } = null;


    public SqlUnitOfWork()
    {
        // ToDo: plumb this into config
        Connection = new SqlConnection("Server=127.0.0.1,11433;Database=BrighterOrderTests;User Id=sa;Password=Password1!;Application Name=BrighterTests;MultipleActiveResultSets=True;encrypt=false");
    }
    
    public async Task BeginTransactionAsync(CancellationToken cancellationToken,
        IsolationLevel isolationLevel = IsolationLevel.Serializable)
    {
        if (Transaction == null)
        {
            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync(cancellationToken);
            Transaction = Connection.BeginTransaction(isolationLevel);
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        if (Transaction == null)
            throw new InvalidOperationException("Transaction has not been started");
        return Transaction.CommitAsync(cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (Transaction == null)
            throw new InvalidOperationException("Transaction has not been started");
        
        return Transaction.RollbackAsync(cancellationToken);
    }
    
    public async Task<SqlCommand> CreateSqlCommandAsync(string sql, SqlParameter[] parameters, CancellationToken cancellationToken)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(cancellationToken);

        var command = Connection.CreateCommand();

        if (Transaction != null)
            command.Transaction = Transaction;

        command.CommandText = sql;
        if(parameters.Length>0)
            command.Parameters.AddRange(parameters); 
        
        return command;
    }
}
