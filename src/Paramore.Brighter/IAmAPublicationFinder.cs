namespace Paramore.Brighter;

/// <summary>
/// Defines an interface for a publication finder.
/// Implementations of this interface are responsible for locating the appropriate
/// <see cref="Publication"/> configuration for a given request type.
/// </summary>
/// <remarks>
/// The default implementation, <c>FindPublicationByRequestType</c>, typically locates
/// publications based on the concrete type of the request. However, by implementing
/// a custom <see cref="IAmAPublicationFinder"/>, users can define alternative strategies
/// for finding publications. This allows for more flexible and context-aware publication
/// configurations.
/// </remarks>
public interface IAmAPublicationFinder
{
    /// <summary>
    /// Finds the <see cref="Publication"/> configuration for the specified request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request (command or event).</typeparam>
    /// <param name="registry">The <see cref="IAmAProducerRegistry"/> containing registered producers and their publications.</param>
    /// <param name="requestContext">The <see cref="RequestContext"/>.</param>
    /// <returns>The <see cref="Publication"/> configuration for the request type, or <c>null</c> if no matching publication is found.</returns>
    Publication Find<TRequest>(IAmAProducerRegistry registry, RequestContext requestContext)
        where TRequest : class, IRequest;
}
