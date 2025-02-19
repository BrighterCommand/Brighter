namespace Paramore.Brighter.MongoDb;

/// <summary>
/// Action to be performed when it's resolving a collection  
/// </summary>
public enum OnResolvingACollection
{
    /// <summary>
    /// Assume the collection exists
    /// </summary>
    Assume,
    
    /// <summary>
    /// Check if the collection, if not throw an exception. 
    /// </summary>
    Validate,
    
    /// <summary>
    /// Check if the collection, if not created
    /// </summary>
    CreateIfNotExists
}
