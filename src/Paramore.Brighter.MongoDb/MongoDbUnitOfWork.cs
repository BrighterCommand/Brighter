using MongoDB.Driver;

namespace Paramore.Brighter.MongoDb;

/// <summary>
/// Represents a unit of work implementation for MongoDB, providing transactional capabilities
/// using MongoDB client sessions. This class manages the lifecycle of a MongoDB session,
/// enabling atomic operations across multiple documents and collections.
/// </summary>
/// <remarks>
/// This unit of work is designed to work with <see cref="IAmAMongoDbConfiguration"/>
/// to obtain the <see cref="IMongoClient"/> and implements <see cref="IAmAMongoDbTransactionProvider"/>
/// to offer both connection and transaction management.
/// </remarks>
/// <param name="configuration">The MongoDB configuration used to obtain the client.</param>
public class MongoDbUnitOfWork(IAmAMongoDbConfiguration configuration) : IAmAMongoDbTransactionProvider, IAmABoxTransactionProvider<IClientSessionHandle>
{
    private IClientSessionHandle? _session;

    /// <inheritdoc />
    public IMongoClient Client { get; } = configuration.Client;
    
    /// <inheritdoc />
    public void Close()
    {
        if (_session == null)
        {
            return;
        }
        
        _session.Dispose();
        _session = null;
    }

    /// <inheritdoc />
    public void Commit()
    {
        if (_session is { IsInTransaction: true })
        {
            _session.CommitTransaction();
        }
        
        _session?.Dispose();
        _session = null;
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_session is { IsInTransaction: true })
        {
            await _session.CommitTransactionAsync(cancellationToken);
        }
        
        _session?.Dispose();
        _session = null;
    }

    /// <inheritdoc />
    public bool HasOpenTransaction => _session is { IsInTransaction: true }; 

    /// <inheritdoc />
    public bool IsSharedConnection => false;
    
    /// <inheritdoc />
    public void Rollback()
    {
        if (_session is { IsInTransaction: true })
        {
            _session.AbortTransaction();
        }

        _session?.Dispose();
        _session = null;
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_session is { IsInTransaction: true })
        {
            await _session.AbortTransactionAsync(cancellationToken);
        }
        
        _session?.Dispose();
        _session = null;
    }

    /// <inheritdoc />
    public IClientSessionHandle GetTransaction()
    {
        _session = Client.StartSession();

        try
        {
            _session.StartTransaction();
        }
        catch (NotSupportedException)
        {
            // In some case mongodb doesn't have support to transaction
            // Like standard alone server
        }
        
        return _session;
    }
    
    /// <inheritdoc />
    public async Task<IClientSessionHandle> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        _session = await Client.StartSessionAsync(cancellationToken: cancellationToken);
        
        try
        {
            _session.StartTransaction();
        }
        catch (NotSupportedException)
        {
            // In some case mongodb doesn't have support to transaction
            // Like standard alone server
        }
        
        return _session;
    }

    async Task<IClientSession> IAmABoxTransactionProvider<IClientSession>.GetTransactionAsync(CancellationToken cancellationToken)
    {
        return await GetTransactionAsync(cancellationToken);
    }

    IClientSession IAmABoxTransactionProvider<IClientSession>.GetTransaction()
    {
        return GetTransaction();
    }
}
