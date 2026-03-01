namespace Paramore.Brighter;

/// <summary>
/// Action to take when scheduling with a duplicate ID
/// </summary>
public enum OnSchedulerConflict
{
    /// <summary>
    /// Throw an <see cref="System.InvalidOperationException"/> if a scheduler with the same ID already exists
    /// </summary>
    Throw,

    /// <summary>
    /// Overwrite the previous scheduler
    /// </summary>
    Overwrite
}
