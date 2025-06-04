namespace Paramore.Brighter.Transforms.Storage;

/// <summary>
/// Represents the configuration options for how items are handled when interacting with a storage mechanism,
/// particularly in the context of the Brighter Claim Check pattern.
/// </summary>
public class StorageOptions
{
    /// <summary>
    /// Gets or sets the strategy to employ when performing a storage operation.
    /// This dictates how the system should react based on the existence of the item in the store.
    /// The default strategy is <see cref="StorageStrategy.CreateIfMissing"/>.
    /// </summary>
    public StorageStrategy Strategy { get; set; } = StorageStrategy.CreateIfMissing;
}
