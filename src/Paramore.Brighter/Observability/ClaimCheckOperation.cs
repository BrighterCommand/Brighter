namespace Paramore.Brighter.Observability;

/// <summary>
/// What is the Claim Check Operation that we are carrying out
/// </summary>
public enum ClaimCheckOperation
{
    /// <summary>
    /// Represents the operation of storing a message payload externally.
    /// This typically involves moving the large data from the message itself to a persistent store.
    /// </summary>
    Store,
    
    /// <summary>
    /// Represents the operation of retrieving a message payload from external storage.
    /// This restores the full message content using the claim check token.
    /// </summary>
    Retrieve,
    
    /// <summary>
    /// Represents the operation of deleting a message payload from external storage.
    /// This is typically performed after the message has been successfully processed and the payload is no longer needed.
    /// </summary>
    Delete,
    
    /// <summary>
    /// Represents the operation of checking if a message payload exists in external storage.
    /// This can be used to verify the presence of a claim before attempting retrieval.
    /// </summary>
    HasClaim
}
