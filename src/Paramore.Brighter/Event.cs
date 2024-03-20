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
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class Event
    /// An event is an indicator to interested parties that 'something has happened'. We expect zero to many receivers as it is one-to-many communication i.e. publish-subscribe
    /// An event is usually fire-and-forget, because we do not know it is received.
    /// </summary>
    public class Event : IEvent
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        [NJsonSchema.Annotations.NotNull]
        public string Id { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        public Event(string id)
        {
            Id = id;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Event"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        public Event(Guid id)
        {
            Id = id.ToString();
        }
        
        /// <summary>
        /// Gets or sets the span that this operation live within
        /// </summary>
        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        [NJsonSchema.Annotations.JsonSchemaIgnore]
        public Activity Span { get; set; }
    }
}
