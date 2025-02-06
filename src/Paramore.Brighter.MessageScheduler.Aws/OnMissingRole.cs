namespace Paramore.Brighter.MessageScheduler.Aws;

/// <summary>
/// Action to be performed when checking role 
/// </summary>
public enum OnMissingRole
{
    /// <summary>
    /// Assume the role if it exists
    /// </summary>
    AssumeRole, 
    /// <summary>
    /// Create the role if it does not exist
    /// </summary>
    CreateRole
}

