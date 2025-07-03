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
    private static readonly ConcurrentDictionary<Type, RoutingKey?> s_typeRoutingKeyCache = new();
    
    /// <inheritdoc cref="IAmAPublicationFinder"/>
    public virtual Publication Find<TRequest>(IAmAProducerRegistry registry, RequestContext context) where TRequest : class, IRequest
    {
        if (context.Topic != null)
        {
            var producer = registry.Producers.FirstOrDefault(x => context.Topic == x.Publication.Topic!);
            if (producer != null)
            {
                return producer.Publication;
            }
        }
        
        var routingKey = s_typeRoutingKeyCache.GetOrAdd(typeof(TRequest), GetRoutingKey);
        if (routingKey != null)
        {
            var producer = registry.Producers.FirstOrDefault(x => routingKey == x.Publication.Topic!);
            if (producer != null)
            {
                return producer.Publication;
            }
        }
        
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
    

    private static RoutingKey? GetRoutingKey(Type requestType)
    {
        var attribute = requestType.GetCustomAttribute<PublicationTopicAttribute>();
        return attribute == null ? null : new RoutingKey(attribute.Topic);
    }
}
