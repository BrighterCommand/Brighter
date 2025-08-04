using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Paramore.Brighter;

/// <summary>
/// An implementation of <see cref="IAmAPublicationFinder"/> that attempts to find a
/// <see cref="Publication"/> first by checking for the presence of the
/// <see cref="PublicationTopicAttribute"/> on the request type. If the attribute is found,
/// it uses the specified topic to locate the publication. If the attribute is not present
/// or no matching publication is found by topic, it falls back to the default behavior
/// of finding a publication by matching the request type.
/// </summary>
/// <remarks>
/// <para>
/// This publication finder provides a flexible approach to configuring message publications.
/// It allows you to explicitly define a topic for a command or event using the
/// <see cref="PublicationTopicAttribute"/>. This is particularly useful in publish/subscribe
/// scenarios where routing is based on topics.
/// </para>
/// <para>
/// If a request type is not decorated with the <see cref="PublicationTopicAttribute"/>,
/// or if no producer is configured to publish to the specified topic, this finder will
/// revert to the standard Brighter behavior of finding a publication based on the
/// exact <see cref="Publication.RequestType"/>. This ensures backward compatibility
/// and allows for a mixed configuration strategy.
/// </para>
/// <para>
/// The class utilizes a static <see cref="ConcurrentDictionary{TKey,TValue}"/> to cache the
/// routing key (derived from the <see cref="PublicationTopicAttribute"/>) for each request
/// type. This improves performance by avoiding repeated reflection lookups for the same
/// request type.
/// </para>
/// </remarks>
public class FindPublicationByPublicationTopicOrRequestType : IAmAPublicationFinder 
{
    private static readonly ConcurrentDictionary<Type, ProducerKey?> s_typeProducerKeyCache = new();
    
    /// <summary>
    /// Finds the <see cref="Publication"/> configuration for the specified request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request (command or event).</typeparam>
    /// <param name="registry">The <see cref="IAmAProducerRegistry"/> containing registered producers and their publications.</param>
    /// <param name="requestContext">The <see cref="RequestContext"/>.</param>
    /// <returns>The <see cref="Publication"/> configuration for the request type, or <c>null</c> if no matching publication is found.</returns>
    public virtual Publication Find<TRequest>(IAmAProducerRegistry registry, RequestContext requestContext) where TRequest : class, IRequest
    {
        if (requestContext.Destination != null)
        {
            var producers = registry.Producers.Where(x => requestContext.Destination.RoutingKey== x.Publication.Topic!).ToArray();
            
            if (producers.Length == 1)
                return producers.First().Publication;
            
            if (producers.Length > 1)
            {
                var producer = producers.FirstOrDefault(x => x.Publication.Type == requestContext.Destination.Type);
                if (producer != null)
                    return producer.Publication;
            }
        }
        
        //Do we have a publication topic attribute for the routing key? If so cache and use it!
        var producerKey = s_typeProducerKeyCache.GetOrAdd(typeof(TRequest), GetDestinationKey);
        if (producerKey != null)
        {
            var producer = registry.Producers.FirstOrDefault(x => x.Publication.Topic! == producerKey.RoutingKey && x.Publication.Type == producerKey.Type);
            if (producer != null)
                return producer.Publication;
        }
        
        //If not attribute-based, then find the publication by matching this requesttype and the publication request type
        var publications = registry.Producers.Select( x=> x.Publication)
            .Where(x=> x.RequestType == typeof(TRequest))
            .ToArray();

        return publications.Length switch
        {
            0 => throw new ConfigurationException("No producer found for request type. Have you set the request type on the Publication?"),
            1 => publications[0],
            _ => throw new ConfigurationException("Only one producer per request type  is supported. Have you added the request type to multiple Publications?")
        };
    }
    

    private static ProducerKey? GetDestinationKey(Type requestType)
    {
        var attribute = requestType.GetCustomAttribute<PublicationTopicAttribute>();
        return attribute?.Destination;
    }
}
