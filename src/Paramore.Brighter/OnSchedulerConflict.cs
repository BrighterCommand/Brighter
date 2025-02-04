namespace Paramore.Brighter;

/// <summary>
/// Action that should be executed when there is a conflict during create a scheduler
/// </summary>
public enum OnSchedulerConflict
{
    Throw,

    /// <summary>
    /// Overwrite the previous scheduler
    /// </summary>
    Overwrite
}
