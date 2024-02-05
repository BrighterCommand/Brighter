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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Transforms.Storage
{
    /// <summary>
    /// Lightweight schema registry, mainly intended for testing as it does not persist into a shared schema registry.
    /// </summary>
    public class InMemorySchemaRegistry : IAmASchemaRegistry
    {
        private readonly ConcurrentDictionary<string, List<BrighterMessageSchema>> _schemas = new ConcurrentDictionary<string, List<BrighterMessageSchema>>();

        private readonly ConcurrentDictionary<int, BrighterMessageSchema> _schemasById = new ConcurrentDictionary<int, BrighterMessageSchema>();
        private static int s_schemaId = 1;

        public Task<(bool, BrighterMessageSchema)> GetAsync(int schemaId)
        {
            var tcs = new TaskCompletionSource<(bool, BrighterMessageSchema)>();
            if (_schemasById.TryGetValue(schemaId, out BrighterMessageSchema schema))
            {
                tcs.SetResult((true, schema));
            }

            tcs.SetResult((false, null));
            return tcs.Task;
        }

        /// <summary>
        /// Looks up a schema for a topic
        /// </summary>
        /// <param name="topic">The topic to find the schema for. </param>
        /// <param name="latestOnly">Only the highest version schema, or all schemas.</param>
        /// <returns>A tuple, with a boolean indicating if the schema could be found and a list of schemas that match</returns>
        public Task<(bool, IEnumerable<BrighterMessageSchema>)> LookupAsync(string topic, bool latestOnly = true)
        {
            var tcs = new TaskCompletionSource<(bool, IEnumerable<BrighterMessageSchema>)>();
            var schemaFound = _schemas.TryGetValue(topic, out List<BrighterMessageSchema> messageSchemas);

            if (schemaFound && latestOnly) 
                tcs.SetResult(
                    (true, 
                    new List<BrighterMessageSchema> {messageSchemas.OrderByDescending(s => s.Version).FirstOrDefault()})
                    ); 

            tcs.SetResult((schemaFound, schemaFound ? messageSchemas : null));

            return tcs.Task;
        }

        /// <summary>
        /// Register a schema
        /// </summary>
        /// <param name="topic">The topic to use as an identifier for the schema</param>
        /// <param name="messageSchema">The JSON schema that you want to register</param>
        /// <param name="newVersion">Explicitly set the new version; if omitted will be 1 if not exists, or highest version + 1</param>
        public Task<int> RegisterAsync(string topic, string messageSchema, int newVersion = -1)
        {
            var tcs = new TaskCompletionSource<int>();
            BrighterMessageSchema registeredSchema = null;
            var found = _schemas.TryGetValue(topic, out List<BrighterMessageSchema> schemas);
            if (found)
            {
                var id = schemas.Select(s => s.Id).First();
                var version = newVersion == -1 ? schemas.Select(s => s.Version).Max() + 1 : newVersion;
                registeredSchema = new BrighterMessageSchema(topic, id, version, messageSchema);
                schemas.Add(registeredSchema);
            }
            else
            {
                registeredSchema= new BrighterMessageSchema(topic, Interlocked.Increment(ref s_schemaId), 1, messageSchema);
                _schemas.TryAdd(
                    topic, 
                    new List<BrighterMessageSchema>
                    {
                        registeredSchema
                    });
            }
            
            _schemasById.TryAdd(registeredSchema.Id, registeredSchema);
            tcs.SetResult(registeredSchema.Id);
            return tcs.Task;

        }

        /// <summary>
        /// Resets the registry, clearing out the registered schemas and resetting ids
        /// </summary>
        public void ClearSchemas()
        {
            _schemas.Clear();
            s_schemaId = 0; 
        }
    }
}
