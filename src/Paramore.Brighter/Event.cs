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
using System.Text.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Annotations;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.NJsonConverters;

namespace Paramore.Brighter;

/// <summary>
/// Class Event
/// An event is an indicator to interested parties that 'something has happened'. We expect zero to many receivers as it is one-to-many communication i.e. publish-subscribe.
/// An event is usually fire-and-forget, because we do not know it is received.
/// </summary>
/// <remarks>
/// Events represent notifications of state changes or domain events that have occurred.
/// They follow the publish-subscribe pattern and can have multiple subscribers.
/// </remarks>
public class Event : IEvent
{
    /// <summary>
    /// Correlates this command with a previous command or event.
    /// </summary>
    /// <value>The <see cref="Id"/> that correlates this command with a previous command or event.</value>
    [JsonConverter(typeof(IdConverter))]
    [Newtonsoft.Json.JsonConverter(typeof(NIdConverter))]
    [JsonSchema(JsonObjectType.String)]
    public Id? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>The <see cref="Id"/> that uniquely identifies this event instance.</value>
    [NotNull]
    [JsonConverter(typeof(IdConverter))]
    [Newtonsoft.Json.JsonConverter(typeof(NIdConverter))]
    [JsonSchema(JsonObjectType.String)]
    public Id Id { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Event"/> class.
    /// </summary>
    /// <param name="id">The <see cref="Id"/> that uniquely identifies this event.</param>
    public Event(Id id)
    {
        Id = id;
    }
        
    /// <summary>
    /// Initializes a new instance of the <see cref="Event"/> class.
    /// </summary>
    /// <param name="id">The <see cref="Guid"/> that will be converted to an <see cref="Id"/> for this event.</param>
    public Event(Guid id)
    {
        Id = new Id(id.ToString());
    }
}
