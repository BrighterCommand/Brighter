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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NJsonSchema;
using NJsonSchema.Generation;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transforms.Transformers
{
    /// <summary>
    /// Where we have a schema id, should we:
    /// - Header: Include it in the header of the message
    /// - Payload: Include it in the payload of the message
    /// </summary>
    public enum SchemaIdStrategy
    {
        /// <summary>
        /// Include the schema id in the header of the message
        /// </summary>
        Header,

        /// <summary>
        /// Include the schema id in the first 4 bytes of payload of the messages
        /// Kafka Connect will use this to determine the schema
        /// </summary>
        Payload
    }

    /// <summary>
    /// The schema registry transformer offers the ability to register the schema of a message with a registry
    /// Optionally it can also validate the message against the schema
    /// </summary>
    public class SchemaRegistryTransformer : IAmAMessageTransformAsync
    {
        private readonly IAmASchemaRegistry _schemaRegistry;
        private readonly JsonSchemaGeneratorSettings _jsonSchemaGeneratorSettings;
        private int _schemaVersion;
        private Type _requestType;
        private bool _validateSchema;
        private bool _latestOnly;
        private SchemaIdStrategy _schemaIdStrategy;

        /// <summary>
        /// Constructs an instance of the SchemaRegistryTransformer
        /// </summary>
        /// <param name="schemaRegistry">The schema registry that we should register schemas with.</param>
        /// <param name="jsonSchemaGeneratorSettings">Settings for how we turn types into JSON Schema when registering</param>
        public SchemaRegistryTransformer(
            IAmASchemaRegistry schemaRegistry,
            JsonSchemaGeneratorSettings jsonSchemaGeneratorSettings)
        {
            _schemaRegistry = schemaRegistry;
            _jsonSchemaGeneratorSettings = jsonSchemaGeneratorSettings;
        }


        /// <summary>
        /// Release any unmanaged resources
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// We need to initialize the transform with the following
        /// [0] - The type derived from <see cref="IRequest"/> that we want to register the schema of
        /// </summary>
        /// <param name="initializerList"></param>
        public void InitializeWrapFromAttributeParams(params object[] initializerList)
        {
            _requestType = (Type)initializerList[0];
            if (initializerList.Length > 1)
            {
                _schemaVersion = (int)initializerList[1];
                _latestOnly = false;
            }
            else
            {
                _latestOnly = true;
            }

            if (initializerList.Length > 2)
                _validateSchema = (bool)initializerList[2];
            else
                _validateSchema = false;

            if (initializerList.Length > 3)
                _schemaIdStrategy = (SchemaIdStrategy)initializerList[3];
        }

        /// <summary>
        /// Unused as we do not recommend validating the schema on receipt, but instead being a tolerant reader
        /// </summary>
        /// <param name="initializerList">Empty</param>
        public void InitializeUnwrapFromAttributeParams(params object[] initializerList)
        {
        }

        /// <summary>
        /// Looks to see if we have a schema registered for this message. If not it will register the appropriate schema.
        /// If the parameter is set, we will also validate the message against the stored schema
        /// </summary>
        /// <param name="message">The message to validate against the schema</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        public async Task<Message> WrapAsync(Message message, CancellationToken cancellationToken = default)
        {
            JsonSchema schema = null;
            int id = -1;
            var (found, schemas) = await _schemaRegistry.LookupAsync(message.Header.Topic, _latestOnly);
            if (!found)
            {
                (id, schema) = await RegisterSchemaAsync(message);
            }
            else
            {
                bool versionExists = schemas.Any(s => s.Version == _schemaVersion);
                if (!versionExists)
                {
                    (id, schema) = await RegisterSchemaAsync(message, _schemaVersion);
                }
                else
                {
                    id = schemas.First(s => s.Version == _schemaVersion).Id;
                }
            }

            message.Header.SchemaId = id;

            if (_validateSchema && schema != null)
            {
                var err = schema.Validate(message.Body.Value);
                if (err.Any())
                {
                    var errMsg = string.Join("|", err.Select(e => e.ToString()).ToArray());
                    throw new InvalidSchemaException(errMsg);
                }
            }

            return message;
        }

        /// <summary>
        /// Unused as we do not recommend validating the schema on receipt, but instead being a tolerant reader 
        /// </summary>
        /// <param name="message">The message passed to the transform</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        public async Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken = default)
        {
            return message;
        }

        private async Task<(int, JsonSchema)> RegisterSchemaAsync(Message message, int? version = null)
        {
            var generator =
                new JsonSchemaGenerator(_jsonSchemaGeneratorSettings ?? JsonSchemaGenerationSettings.Default);
            var schema = generator.Generate(_requestType);

            var id = version.HasValue
                ? await _schemaRegistry.RegisterAsync(message.Header.Topic, schema.ToJson(), version.Value)
                : await _schemaRegistry.RegisterAsync(message.Header.Topic, schema.ToJson(), -1);

            return (id, schema);
        }
    }
}
