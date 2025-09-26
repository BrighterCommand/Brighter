namespace Paramore.Brighter.MessageScheduler.AWS;

/// <summary>
/// Action to be performed when checking role 
/// </summary>
public enum OnMissingRole
{
    /// <summary>
    /// Assume the role if it exists
    /// </summary>
    Assume, 
    
    /// <summary>
    /// Check if the role exists,
    /// </summary>
    Validate,
    
    /// <summary>
    /// Create the role if it does not exist
    /// </summary>
    Create
}

