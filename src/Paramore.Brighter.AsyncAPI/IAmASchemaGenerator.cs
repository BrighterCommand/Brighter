#region Licence
/* The MIT License (MIT)
Copyright © 2026 Jonny Olliff-Lee <jonny.ollifflee@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Neuroglia.AsyncApi.v3;

namespace Paramore.Brighter.AsyncAPI
{
    /// <summary>
    /// Generates JSON Schema representations for message payload types used in AsyncAPI document generation.
    /// Implementations should return a <see cref="V3SchemaDefinition"/> wrapping the schema.
    /// When <paramref name="requestType"/> is null or schema generation fails, implementations should
    /// return an empty object schema rather than null.
    /// </summary>
    public interface IAmASchemaGenerator
    {
        /// <summary>
        /// Generates a JSON Schema for the specified request type.
        /// </summary>
        /// <param name="requestType">The type to generate a schema for, or null to produce an empty object schema.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// A <see cref="V3SchemaDefinition"/> containing the JSON Schema. Implementations should return
        /// an empty object schema (not null) for null <paramref name="requestType"/> or on error.
        /// The caller will fall back to an empty object schema if null is returned.
        /// </returns>
        Task<V3SchemaDefinition?> GenerateAsync(Type? requestType, CancellationToken ct = default);
    }
}
