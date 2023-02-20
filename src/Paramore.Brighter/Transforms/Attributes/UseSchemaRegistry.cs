#region Licence
/* The MIT License (MIT)
Copyright © 2023 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Transforms.Attributes
{
    /// <summary>
    /// Adds schema registration to the step of mapping an outgoing message
    /// Only has a wrap not an unwrap as we assume a tolerant reader approach: https://www.martinfowler.com/bliki/TolerantReader.html
    /// We are therefore strict about what we send, but tolerate what we receive.
    /// </summary>
    public class UseSchemaRegistry : WrapWithAttribute
    {
        private readonly Type _requestType;
        private readonly int? _schemaVersion;
        private readonly bool? _validateSchema;

        /// <summary>
        /// Configures how the schema registry should be used
        /// </summary>
        /// <param name="step">The order to run the schema capture and validation in</param>
        /// <param name="requestType">The type of the request for this handler, used to determine schema</param>
        /// <param name="schemaVersion">The version of the registered schema i.e. to register if missing, to use for validation</param>
        /// <param name="validateSchema">Validate that a message conforms to the outgoing schema. Will throw an InvalidSchemaException from the transform if fails</param>
        public UseSchemaRegistry(int step, Type requestType, int? schemaVersion = null, bool? validateSchema = null) : base(step)
        {
            _requestType = requestType;
            _schemaVersion = schemaVersion;
            _validateSchema = validateSchema;
        }

        /// <summary>
        /// Passes the configuration values to the handler
        /// </summary>
        /// <returns>The configuration values as an object array</returns>
        public override object[] InitializerParams()
        {
            var attribs = new ArrayList { _requestType };
            if (_schemaVersion.HasValue) attribs.Add(_schemaVersion.Value);
            if (_validateSchema.HasValue) attribs.Add(_validateSchema.Value);

            return attribs.ToArray();
        }


        /// <summary>
        /// The transformer type to instantiate, in this case <see cref="SchemaRegistryTransformer"/>
        /// </summary>
        /// <returns>The type of <see cref="SchemaRegistryTransformer"/></returns>
        public override Type GetHandlerType()
        {
            return typeof(SchemaRegistryTransformer);
        }
    }
}
