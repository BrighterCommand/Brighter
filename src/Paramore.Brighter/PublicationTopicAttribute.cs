using System;

namespace Paramore.Brighter;

/// <summary>
/// An attribute that allows you to specify the topic for a command or event type when using a
/// publish/subscribe messaging pattern within the Brighter framework. This attribute can be
/// used by custom <see cref="IAmAPublicationFinder"/> implementations to route messages based on topic.
/// </summary>
/// <remarks>
/// <para>
/// In publish/subscribe systems, messages are published to a topic, and consumers subscribe to topics
/// they are interested in. Applying this attribute to your command or event classes allows
/// publication finders, such as <see cref="FindPublicationByPublicationTopicOrRequestType"/>,
/// to identify the appropriate topic for publishing messages of that type.
/// </para>
/// </remarks>
/// <example>
/// [PublicationTopic("user.events")]
/// public class UserCreatedEvent : Event
/// {
///     public Guid UserId { get; set; }
///     public string UserName { get; set; }
/// }
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class PublicationTopicAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PublicationTopicAttribute"/> class.
    /// </summary>
    /// <param name="topic">The topic associated with the command or event type.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="topic"/> is null or empty.</exception>
    public PublicationTopicAttribute(string topic)
    {
        if (string.IsNullOrEmpty(topic))
        {
            throw new ArgumentException("topic cannot be null or empty");
        }
        
        Topic = topic;
    }
    
    /// <summary>
    /// Gets or sets the topic specified by the attribute.
    /// </summary>
    /// <value>
    /// The topic as a <see cref="string"/>.
    /// </value>
    public string Topic { get; set; }
}
