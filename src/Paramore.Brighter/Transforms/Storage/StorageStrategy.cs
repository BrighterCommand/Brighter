namespace Paramore.Brighter.Transforms.Storage;

/// <summary>
/// Defines strategies for handling the existence of an item when interacting with a storage mechanism.
/// These strategies dictate how a storage operation should proceed based on whether
/// the item already exists or is missing from the store.
/// </summary>
public enum StorageStrategy
{
    /// <summary>
    /// If the item does not exist in the store, it will be created or added.
    /// </summary>
    CreateIfMissing,
    
    /// <summary>
    /// Validates the existence of the item in the store.
    /// If the item does not exist, the operation will throw an exception,
    /// indicating that the prerequisite for the operation (item existence) was not met.
    /// </summary>
    Validate,
    
    /// <summary>
    /// Assumes the item exists in the store and proceeds with the operation without
    /// explicitly checking for its presence.
    /// </summary>
    Assume
}
