using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.Spanner;

/// <summary>
/// Represents a Unit of Work for Google Cloud Spanner, managing database connections and transactions.
/// This class provides mechanisms to obtain and manage <see cref="SpannerConnection"/> and
/// <see cref="SpannerTransaction"/> instances, ensuring that all operations within a logical
/// business transaction are atomic.
/// </summary>
/// <remarks>
/// This class extends <see cref="RelationalDbTransactionProvider"/> and is designed to encapsulate
/// the complexities of managing Spanner connections and transactions, including handling their
/// state (open/closed) and ensuring proper transaction lifecycle (begin, commit, rollback).
/// </remarks>
/// <param name="configuration">The configuration containing the connection string for the Spanner database.</param>
public class SpannerUnitOfWork(IAmARelationalDatabaseConfiguration configuration) : RelationalDbTransactionProvider
{
    private readonly string _connectionString = configuration.ConnectionString;
    
    /// <inheritdoc />
    public override DbConnection GetConnection()
    {
        Connection ??= new SpannerConnection(_connectionString);

        if (Connection.State != ConnectionState.Open)
        {
            Connection.Open();
        }
        
        return Connection;
    }

    /// <inheritdoc />
    public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        Connection ??= new SpannerConnection(_connectionString);

        if (Connection.State != ConnectionState.Open)
        {
            await Connection.OpenAsync(cancellationToken);
        }
        
        return Connection;
    }

    /// <inheritdoc />
    public override async Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        Connection ??= await GetConnectionAsync(cancellationToken);

        if (!HasOpenTransaction)
        {
            Transaction = await ((SpannerConnection)Connection).BeginTransactionAsync(cancellationToken);
        }

        return Transaction!;
    }

    /// <inheritdoc />
    public override async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (HasOpenTransaction)
        {
            await ((SpannerTransaction)Transaction!).CommitAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (HasOpenTransaction)
        {
            await ((SpannerTransaction)Transaction!).RollbackAsync(cancellationToken);
        }
    }
}
