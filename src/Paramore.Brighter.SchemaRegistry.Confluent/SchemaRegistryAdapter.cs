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


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.SchemaRegistry.Confluent
{
    /// <summary>
    /// Adapts the Confluent Schema Registry to the Brighter schema abstraction
    /// </summary>
    public class SchemaRegistryAdapter : IAmASchemaRegistry
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<SchemaRegistryAdapter>();
        
        private readonly List<SchemaReference> _emptyReferencesList = new List<SchemaReference>();
        private readonly CachedSchemaRegistryClient _schemaRegistryClient;
        private readonly Func<string, string> _namingStrategy;

        public SchemaRegistryAdapter(SchemaRegistryConfig schemaRegistryConfig, Func<string, string> namingStrategy = null)
        {
            _schemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);

            if (namingStrategy == null)
                _namingStrategy = (s) => s + "-value";
            else 
                _namingStrategy = namingStrategy;

        }

        public async Task<(bool, IEnumerable<BrighterMessageSchema>)> LookupAsync(string topic, bool latestOnly = true)
        {
            var found = true;
            var schemas = new List<BrighterMessageSchema>();
            try
            {
                if (latestOnly)
                {
                    var schema = await _schemaRegistryClient.GetLatestSchemaAsync(topic);
                    schemas.Add(new BrighterMessageSchema(schema.Subject, schema.Id, schema.Version, schema.SchemaString));
                }
                else
                {
                    var schemaIds = await _schemaRegistryClient.GetSubjectVersionsAsync(topic);
                    schemaIds.ForEach(async i =>
                    {
                        var schema = await _schemaRegistryClient.GetSchemaAsync(i);
                        schemas.Add(new BrighterMessageSchema(schema.Subject, schema.Id, schema.Version, schema.SchemaString)); 
                    });
                }
            }
            catch (SchemaRegistryException se)
            {   
                s_logger.LogInformation("Schema for {topic} was not in schema registry, will register", topic);
                found = false;
            }

            return (found, schemas);
        }

        public async Task<int> RegisterAsync(string topic, string messageSchema, int version)
        {
            s_logger.LogInformation("Schema for {topic} was not in schema registry, will register", topic);
            
            var registeredSchema = new RegisteredSchema(subject: topic, version: 0, id: 0, schemaString: messageSchema, schemaType: SchemaType.Json, _emptyReferencesList);
            // dotnet api uses the obsolete schema, which does not hold version or id, and is used for unregistered schema
            var id = await _schemaRegistryClient.RegisterSchemaAsync(_namingStrategy(topic), registeredSchema.Schema);

            return id;
        }

        /// <summary>
        /// Clears the schemas from the registry
        /// </summary>
        public void ClearSchemas()
        {
            //Clear the schemas in Confluent Schema Registry
            
        }
    }
}
