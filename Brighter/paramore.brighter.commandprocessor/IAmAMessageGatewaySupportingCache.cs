using System.Threading;

public interface IAmAMessageGatewaySupportingCache
{
    /// <summary>
    /// Gets if the current provider configuration is able to support cached retrieval of messages.
    /// </summary>
    bool CacheSupported { get; }
}