#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Paramore.Brighter.Transforms.Storage
{
    public interface IAmASchemaRegistry
    {
        /// <summary>
        /// Lookup the schema history for this topic
        /// </summary>
        /// <param name="topic">The topic to find registered schemas for</param>
        /// <param name="latestOnly">Only returs the latest schema</param>
        /// <returns>The set of schemas for this topic</returns>
        Task<(bool, IEnumerable<BrighterMessageSchema>)> LookupAsync(string topic, bool latestOnly = true);

        /// <summary>
        /// Register a schema for this topic with the registry
        /// </summary>
        /// <param name="topic">The topic to register under</param>
        /// <param name="messageSchema">The schema to register</param>
        /// <param name="newVersion">An explicit version to register. Pass -1 to figure out the version as highest existing + 1</param>
        /// <returns></returns>
        Task<int> RegisterAsync(string topic, string messageSchema, int newVersion);

        /// <summary>
        /// Clears the schemas from the registry
        /// </summary>
        void ClearSchemas();
    }
}
