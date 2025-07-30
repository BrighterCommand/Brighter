#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Net.Mime;

namespace Paramore.Brighter
{
    /// <summary>
    /// Contains configuration that is generic to producers - similar to Subscription for consumers
    /// Unlike <see cref="Subscription"/>, as it is passed to a constructor it is by convention over enforced at compile time
    /// Platform specific configuration goes into a <see cref="IAmGatewayConfiguration"/> derived class
    /// </summary>
    public class Publication
    {
        /// <summary>
        /// OPTIONAL
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">Cloud Events Spec</see>
        /// Identifies the schema that data adheres to. Incompatible changes to the schema SHOULD be reflected by a different URI. 
        /// </summary>
        public Uri? DataSchema { get; set; }
        
        /// <summary>
        /// What do we do with infrastructure dependencies for the producer?
        /// </summary>
        public OnMissingChannel MakeChannels { get; set; }
        
        /// <summary>
        /// The type of the request that we expect to publish on this channel
        /// </summary>
        public Type? RequestType { get; set; }
        
        /// <summary>
        /// REQUIRED
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">Cloud Events Spec</see>
        /// Identifies the context in which an event happened. Often this will include information such as the type of
        /// the event source, the organization publishing the event or the process that produced the event.
        /// The exact syntax and semantics behind the data encoded in the URI is defined by the event producer.
        /// Producers MUST ensure that source + id is unique for each distinct event.
        /// Default: "http://goparamore.io" for backward compatibility as required
        /// </summary>
        public Uri Source { get; set; } = new Uri("http://goparamore.io");
        
        /// <summary>
        /// OPTIONAL
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">Cloud Events Spec</see>
        /// This describes the subject of the event in the context of the event producer (identified by source).
        /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source,
        /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the
        /// source context has internal sub-structure.
        /// </summary>
        public string? Subject { get; set; }
        
        /// <summary>
        /// The topic this publication is for defined by a <see cref="RoutingKey"/>
        /// </summary>
        /// <remarks>
        /// In a pub-sub scenario there is typically a topic, to which we publish and then a subscriber creates its own
        /// queue which the broker delivers messages to. Typically, the <see cref="ChannelName"/> on the Subscription
        /// will be used as that queue name.
        /// For point-to-point scenarios, implementers will need to add a <see cref="ChannelName"/> to their derived <see cref="Publication"/>
        /// to hold the name of the queue being used for message exchange, or  a platform dependent proxy for that (for example a URI)
        /// </remarks>
        public RoutingKey? Topic { get; set; }

        /// <summary>
        /// REQUIRED
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">Clode Events Spec</see>
        /// This attribute contains a value describing the type of event related to the originating occurrence.
        /// Often this attribute is used for routing, observability, policy enforcement, etc.
        /// SHOULD be prefixed with a reverse-DNS name. The prefixed domain dictates the organization which defines the semantics of this event type.
        /// Default: "goparamore.io.Paramore.Brighter.Message" for backward compatibility as required
        /// </summary>
        public CloudEventsType Type { get; set; } = CloudEventsType.Empty; 
        
        /// <summary>
        /// Gets or sets the default headers to be included in published messages when using default message mappers.
        /// </summary>
        /// <remarks>
        /// These headers will be automatically added to all messages published through Brighter's message producers.
        /// <para>
        /// Default message mappers will use these headers when constructing the outgoing message envelope.
        /// </para>
        /// <para>
        /// Headers should be structured as key-value pairs where:
        /// <list type="bullet">
        /// <item><description>Key: Header name (string)</description></item>
        /// <item><description>Value: Header value (object)</description></item>
        /// </list>
        /// </para>
        /// <example>
        /// Setting default headers:
        /// <code>
        /// publication.DefaultHeaders = new Dictionary&lt;string, object&gt;
        /// {
        ///     ["x-correlation-id"] = Guid.NewGuid(),
        ///     ["x-message-type"] = typeof(MyEvent).FullName
        /// };
        /// </code>
        /// </example>
        /// <para>
        /// If no default headers are required, this property can be left as <c>null</c>.
        /// </para>
        /// <para>
        /// Note: These headers are only applied when using Brighter's default message mapping pipeline. 
        /// Custom mappers may ignore this property.
        /// </para>
        /// </remarks>
        public IDictionary<string, object>? DefaultHeaders { get; set; }
        
        /// <summary>
        /// Gets or sets a dictionary of additional properties related to CloudEvents.
        /// This property enables the inclusion of custom or vendor-specific metadata beyond the standard CloudEvents attributes.
        /// These properties are serialized alongside the core CloudEvents attributes when mapping to a CloudEvent message.
        /// </summary>
        /// <value>
        /// A dictionary where keys represent the names of the additional CloudEvents attributes and values are their corresponding data.
        /// This can be <c>null</c> if no additional properties are provided.
        /// </value>
        /// <remarks>
        /// Use this dictionary to attach any non-standard CloudEvents attributes pertinent to your specific application or integration requirements.
        /// During serialization to a CloudEvent JSON structure, the key-value pairs within this dictionary are added as top-level properties in the resulting JSON.
        /// This mechanism facilitates forward compatibility and allows for seamless extensibility in accordance with the CloudEvents specification and its extensions.
        /// <para>
        /// This property is utilized by the <see cref="Paramore.Brighter.MessageMappers.CloudEventJsonMessageMapper{T}"/>
        /// for mapping and by the <see cref="Paramore.Brighter.Transforms.Transformers.CloudEventsTransformer"/> for
        /// transforming messages into CloudEvents.
        /// </para>
        /// <para>
        /// **Important:** If any key in this dictionary conflicts with the name of a standard CloudEvents JSON property (e.g., "id", "source", "type"),
        /// the serializer (<c>System.Text.Json</c>) will prioritize the value present in this <c>CloudEventsAdditionalProperties</c> dictionary,
        /// effectively overriding the standard property's value during serialization. Exercise caution to avoid unintended
        /// overwrites of core CloudEvents attributes.
        /// </para>
        /// </remarks> 
        public IDictionary<string, object>? CloudEventsAdditionalProperties { get; set; }
        
        /// <summary>
        /// OPTIONAL
        /// Gets or sets the reply to topic. Used when doing Request-Reply instead of Publish-Subscribe to identify
        /// the queue that the sender is listening on. Usually a sender listens on a private queue, so that they
        /// do not have to filter replies intended for other listeners.
        /// </summary>
        /// <value>The reply to.</value>
        public string? ReplyTo { get; set; }
    }

    /// <summary>
    /// Contains configuration that is generic to producers - similar to Subscription for consumers
    /// Unlike <see cref="Subscription"/>, as it is passed to a constructor it is by convention over enforced at compile time
    /// Platform specific configuration goes into a <see cref="IAmGatewayConfiguration"/> derived class
    /// </summary>
    public class Publication<T> : Publication
        where T: class, IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Publication{T}"/> class. 
        /// </summary>
        public Publication()
        {
            RequestType = typeof(T);
        }
    }
}
