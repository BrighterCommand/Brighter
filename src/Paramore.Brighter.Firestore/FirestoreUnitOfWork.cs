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
public class FirestoreUnitOfWork : FirestoreConnectionProvider, IAmAFirestoreTransactionProvider
{
    private FirestoreTransaction? _transaction;
    private readonly FirestoreConfiguration _configuration;

    /// <summary>
    /// Provides a concrete implementation for managing Firestore transactions,
    /// adhering to the <see cref="IAmABoxTransactionProvider{TTransaction}"/> interface.
    /// This class handles the low-level interactions with the Firestore API to
    /// begin, commit, and rollback transactions.
    /// </summary>
    public FirestoreUnitOfWork(FirestoreConfiguration configuration) 
        : base(configuration)
    {
        _configuration = configuration;
    }

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
            Database = _configuration.DatabasePath, 
            Transaction = _transaction.Transaction
        };
        
        request.Writes.AddRange(_transaction.Writes);
        var client = GetFirestoreClient();
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
            Database = _configuration.DatabasePath, 
            Transaction = _transaction.Transaction
        };
            
        request.Writes.AddRange(_transaction.Writes);
            
        var client = await GetFirestoreClientAsync(cancellationToken);
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
            Database = _configuration.DatabasePath,
            Transaction = _transaction.Transaction
        };
        
        var client = GetFirestoreClient();
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
            Database = _configuration.DatabasePath,
            Transaction = _transaction.Transaction
        };
        
        var client = await GetFirestoreClientAsync(cancellationToken);
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
        
        var client = GetFirestoreClient();
        var transaction = client.BeginTransaction(new BeginTransactionRequest { Database = _configuration.DatabasePath });
        return _transaction = new FirestoreTransaction(transaction.Transaction);
    }

    /// <inheritdoc />
    public async Task<FirestoreTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            return _transaction;
        }
        
        var client = await GetFirestoreClientAsync(cancellationToken);
        var transaction = await client.BeginTransactionAsync(new BeginTransactionRequest { Database = _configuration.DatabasePath },
            CallSettings.FromCancellationToken(cancellationToken));
        return _transaction = new FirestoreTransaction(transaction.Transaction);
    }
}
