using System.Linq;

namespace Paramore.Brighter;

/// <summary>
/// The default implementation of <see cref="IAmAPublicationFinder"/>, which locates a
/// <see cref="Publication"/> based on the exact type of the request (command or event).
/// </summary>
/// <remarks>
/// <para>
/// This class provides the standard behavior for finding publication configurations in Brighter.
/// It iterates through the registered producers in the <see cref="IAmAProducerRegistry"/> and
/// selects the <see cref="Publication"/> whose <see cref="Publication.RequestType"/> matches
/// the type of the request being published.
/// </para>
/// <para>
/// This implementation enforces a one-to-one mapping between a request type and a publication.
/// If no publication is found for a given request type, or if multiple publications are
/// configured with the same request type, a <see cref="ConfigurationException"/> is thrown.
/// </para>
/// </remarks>
public class FindPublicationByRequestType : IAmAPublicationFinder 
{
    /// <inheritdoc cref="IAmAPublicationFinder"/>
    public virtual Publication Find<TRequest>(IAmAProducerRegistry registry) where TRequest : class, IRequest
    {
        var publications = registry.Producers.Select( x=> x.Publication)
            .Where(x=> x.RequestType == typeof(TRequest))
            .ToArray();

        return publications.Length switch
        {
            0 => throw new ConfigurationException("No producer found for request type. Have you set the request type on the Publication?"),
            1 => publications[0],
            _ => throw new ConfigurationException("Only one producer per request type is supported. Have you added the request type to multiple Publications?")
        };
    }
}
