﻿#region Licence
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

using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NJsonSchema.Generation;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public static class ConfluentJsonSerializationConfig
    {
        public static JsonSerializerConfig SerdesJsonSerializerConfig()
        {
            return new JsonSerializerConfig
            {
                AutoRegisterSchemas = true,
                NormalizeSchemas = false,
                UseLatestVersion = false,
                SubjectNameStrategy = SubjectNameStrategy.Topic
            };
        }

        public static JsonSchemaGeneratorSettings NJsonSchemaGeneratorSettings()
        {
            var resolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() };

            return new JsonSchemaGeneratorSettings
            {
                FlattenInheritanceHierarchy = true,
                SerializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = resolver,
                    Formatting = Formatting.Indented
                }
            };
        }
    }
}
