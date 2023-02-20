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
using System.Net.Http;
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
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<SchemaRegistryAdapter>();
        
        private readonly List<SchemaReference> _emptyReferencesList = new List<SchemaReference>();
        private readonly CachedSchemaRegistryClient _schemaRegistryClient;
        private readonly Func<string, string> _namingStrategy;

        /// <summary>
        /// Creates an adapter around the Confluent Schema Registry, for Brighter's <see cref="IAmASchemaRegistry"/> inteface
        /// </summary>
        /// <param name="schemaRegistryConfig">The configuration for Confluent's Cached Schema Registry</param>
        /// <param name="httpClientFactory">An HTTP Connection for COnfluent Schema Registry API methods not supported by CachedSchemaRegistry</param>
        /// <param name="namingStrategy">The strategy for naming the subject in the registry - defaults to {topic}-value</param>
        public SchemaRegistryAdapter(
            SchemaRegistryConfig schemaRegistryConfig,
            IHttpClientFactory httpClientFactory,
            Func<string, string> namingStrategy = null)
        {
            _httpClientFactory = httpClientFactory;
            _schemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);

            if (namingStrategy == null)
                _namingStrategy = (s) => s + "-value";
            else 
                _namingStrategy = namingStrategy;

        }

       /// <summary>
       /// Lookup the schemas for this topic
       //NOTE: Confluent schema registry only does caching for retrieval by id. In other words, if you know the id
       //of the schema version that you want, then you can pull that from a cache, because it won't be stale
       //if you just know the subject, you don't have a cached result
       /// </summary>
       /// <param name="topic">The topic to find schemas for (subject will be via naming strategy)</param>
       /// <param name="latestOnly">Only return the latest version of the schema</param>
       /// <returns> A tuple: Bool: are there schemas Enumerable: A set of matching schemas</returns>
        public async Task<(bool, IEnumerable<BrighterMessageSchema>)> LookupAsync(string topic, bool latestOnly = true)
        {
           var found = true;
            var schemas = new List<BrighterMessageSchema>();
            try
            {
                if (latestOnly)
                {
                    var schema = await _schemaRegistryClient.GetLatestSchemaAsync(_namingStrategy(topic));
                    schemas.Add(new BrighterMessageSchema(schema.Subject, schema.Id, schema.Version, schema.SchemaString));
                }
                else
                {
                    var schemaIds = await _schemaRegistryClient.GetSubjectVersionsAsync(_namingStrategy(topic));
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
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri($"https://{bucketName}.s3.{bucketRegion.Value}.amazonaws.com");
            using (var headRequest = new HttpRequestMessage(HttpMethod.Head, @"/"))
            {
                headRequest.Headers.Add("x-amz-expected-bucket-owner", accountId);
                var response = await httpClient.SendAsync(headRequest);
                //If we deny public access to the bucket, but it exists we get access denied; we get not-found if it does not exist 
                return (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Forbidden);
            }
            
        }
    }
}
