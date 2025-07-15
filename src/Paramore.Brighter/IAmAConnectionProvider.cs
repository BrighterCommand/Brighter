namespace Paramore.Brighter;

/// <summary>
/// An interface to mark another interface as connection provider (like DynamoDB, RDS and etc)
/// </summary>
/// <remarks>
/// It's used during register a dependency
/// </remarks>
public interface IAmAConnectionProvider;
