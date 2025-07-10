using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Firestore.V1;

namespace Paramore.Brighter.Firestore;

/// <summary>
/// Provides a concrete implementation for managing Firestore transactions,
/// adhering to the <see cref="IAmABoxTransactionProvider{TTransaction}"/> interface.
/// This class handles the low-level interactions with the Firestore API to
/// begin, commit, and rollback transactions.
/// </summary>
public class FirestoreTransactionProvider(FirestoreConfiguration configuration) : IAmABoxTransactionProvider<FirestoreTransaction>
{
    private FirestoreTransaction? _transaction;
    
    /// <inheritdoc />
    public void Close() => Rollback();

    /// <inheritdoc />
    public void Commit()
    {
        if (_transaction == null)
        {
            return;
        }
        
        var request = new CommitRequest
        {
            Database = configuration.Database, 
            Transaction = _transaction.Transaction
        };
        
        request.Writes.AddRange(_transaction.Writes);
        var client = configuration.CreateFirestoreClient();
        client.Commit(request);
        
        _transaction = null;
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            return;
        }
        
        var request = new CommitRequest
        {
            Database = configuration.Database, 
            Transaction = _transaction.Transaction
        };
            
        request.Writes.AddRange(_transaction.Writes);
            
        var client = await configuration.CreateFirestoreClientAsync(cancellationToken);
        await client.CommitAsync(request, CallSettings.FromCancellationToken(cancellationToken));
        
        _transaction = null;
    }

    /// <inheritdoc />
    public bool HasOpenTransaction => _transaction != null;

    /// <inheritdoc />
    public bool IsSharedConnection => true;
    
    /// <inheritdoc />
    public void Rollback()
    {
        if (_transaction == null)
        {
            return;
        }

        var request = new RollbackRequest
        {
            Database = configuration.Database,
            Transaction = _transaction.Transaction
        };
        
        var client = configuration.CreateFirestoreClient();
        client.Rollback(request);
        
        _transaction = null;
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            return;
        }

        var request = new RollbackRequest
        {
            Database = configuration.Database,
            Transaction = _transaction.Transaction
        };
        
        var client = await configuration.CreateFirestoreClientAsync(cancellationToken);
        await client.RollbackAsync(request, CallSettings.FromCancellationToken(cancellationToken));
        
        _transaction = null;
    }

    /// <inheritdoc />
    public FirestoreTransaction GetTransaction()
    {
        if (_transaction != null)
        {
            return _transaction;
        }
        
        var client = configuration.CreateFirestoreClient();
        var transaction = client.BeginTransaction(new BeginTransactionRequest { Database = configuration.Database });
        return _transaction = new FirestoreTransaction(transaction.Transaction);
    }

    /// <inheritdoc />
    public async Task<FirestoreTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            return _transaction;
        }
        
        var client = await configuration.CreateFirestoreClientAsync(cancellationToken);
        var transaction = await client.BeginTransactionAsync(new BeginTransactionRequest { Database = configuration.Database },
            CallSettings.FromCancellationToken(cancellationToken));
        return _transaction = new FirestoreTransaction(transaction.Transaction);
    }
}
