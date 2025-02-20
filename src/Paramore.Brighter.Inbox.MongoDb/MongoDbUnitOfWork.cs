using MongoDB.Driver;

namespace Paramore.Brighter.Inbox.MongoDb;

/// <summary>
/// The MongoDB Unit of work
/// </summary>
/// <param name="client"></param>
public class MongoDbUnitOfWork(IMongoClient client) : IAmABoxTransactionProvider<IClientSessionHandle>
{
    private IClientSessionHandle? _session;

    /// <inheritdoc />
    public void Close()
    {
        if (_session != null)
        {
            _session.Dispose();
            _session = null;
        }
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
        if (_session != null)
        {
            await _session.CommitTransactionAsync(cancellationToken);
            _session.Dispose();
        }

        _session = null;
    }

    /// <inheritdoc />
    public IClientSessionHandle GetTransaction()
    {
        if (_session != null)
        {
            _session = client.StartSession();
        }

        return _session!;
    }

    /// <inheritdoc />
    public async Task<IClientSessionHandle> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_session != null)
        {
            _session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        }

        return _session!;
    }

    /// <inheritdoc />
    public bool HasOpenTransaction => _session != null;

    /// <inheritdoc />
    public bool IsSharedConnection => false;

    /// <inheritdoc />
    public void Rollback()
    {
        if (_session != null)
        {
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
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_session != null)
        {
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
    }
}
