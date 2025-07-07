using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Transforms.Storage;

/// <summary>
/// Represents a **null object implementation** of the <see cref="IAmAStorageProvider"/>
/// and <see cref="IAmAStorageProviderAsync"/> interfaces for the Brighter Claim Check pattern.
/// </summary>
/// <remarks>
/// This class is primarily intended for **initial setup or testing scenarios** where a concrete
/// storage provider has not yet been configured. All operations within this store
/// will explicitly throw a <see cref="System.NotImplementedException"/>, guiding the
/// developer to register a real storage solution. It acts as a placeholder to ensure
/// that any attempt to use the claim check feature without a proper backing store
/// results in an immediate and clear error.
/// <para>
/// **It is crucial to replace this null store with a concrete implementation**
/// (e.g., using <c>UseExternalLuggageStore</c> during Brighter configuration)
/// for any production or functional environment.
/// </para>
/// </remarks>
public class NullLuggageStore : IAmAStorageProvider, IAmAStorageProviderAsync
{
    /// <inheritdoc cref="IAmAStorageProvider.Tracer"/>
    public IAmABrighterTracer? Tracer { get; set; }

    /// <inheritdoc />
    public Task EnsureStoreExistsAsync(CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
    }

    /// <inheritdoc />
    public Task DeleteAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
    }

    /// <inheritdoc />
    public Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
    }

    /// <inheritdoc />
    public Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
    }

    /// <inheritdoc />
    public Task<string> StoreAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
    }

    /// <inheritdoc />
    public void EnsureStoreExists()
    {
        throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
    }

    /// <inheritdoc />
    public void Delete(string claimCheck)
    {
        throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
    }

    /// <inheritdoc />
    public Stream Retrieve(string claimCheck)
    {
        throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
    }

    /// <inheritdoc />
    public bool HasClaim(string claimCheck)
    {
        throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
    }

    /// <inheritdoc />
    public string Store(Stream stream)
    {
        throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
    }
}
