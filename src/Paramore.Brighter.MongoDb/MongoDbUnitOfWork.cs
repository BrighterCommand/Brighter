﻿using MongoDB.Driver;

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
public class MongoDbUnitOfWork(IAmAMongoDbConfiguration configuration) : IAmAMongoDbTransactionProvider
{
    private IClientSession? _session;

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
        _session?.CommitTransaction();
        _session?.Dispose();
        _session = null;
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_session == null)
        {
            return;
        }
        
        await _session.CommitTransactionAsync(cancellationToken);
        _session.Dispose();
        _session = null;
    }

    /// <inheritdoc />
    public bool HasOpenTransaction => _session != null; 

    /// <inheritdoc />
    public bool IsSharedConnection => false;
    
    /// <inheritdoc />
    public void Rollback()
    {
        if (_session == null)
        {
            return;
        }
        
        try
        {
            _session.AbortTransaction();
        }
        catch
        {
            // Ignore
        }

        _session.Dispose();
        _session = null;
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_session == null)
        {
            return;
        }
        
        try
        {
            await _session.AbortTransactionAsync(cancellationToken);
        }
        catch
        {
            // Ignore
        }

        _session.Dispose();
        _session = null;
    }

    /// <inheritdoc />
    public IClientSession GetTransaction()
    {
        return _session = Client.StartSession();
    }

    /// <inheritdoc />
    public async Task<IClientSession> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        return _session = await Client.StartSessionAsync(cancellationToken: cancellationToken);
    }
}
