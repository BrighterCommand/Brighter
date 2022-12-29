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

using System.Text.Json;
using System.Text.Json.Serialization;
using Paramore.Brighter.Serialization;

namespace Paramore.Brighter
{
    /// <summary>
    /// Global Configuration for the Json Serializer
    /// We provide custom type converters, particularly around serializing objects so that they appear as types not JsonElement
    /// The camelCase property will convert a key, such as a header bag key, to camelCase if you use UpperCase and so if you expect
    /// an UpperCase property you will find that it is now a camelCase one. It is best to use camelCase names for this reason
    /// </summary>
    public static class JsonSerialisationOptions
    {
        /// <summary>
        /// Brighter serialization options for use when serializing or deserializing text
        /// </summary>
        public static JsonSerializerOptions Options { get; set; }

        static JsonSerialisationOptions()
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                AllowTrailingCommas = true
            };

            opts.Converters.Add(new JsonStringConverter());
            opts.Converters.Add(new DictionaryStringObjectJsonConverter());
            opts.Converters.Add(new ObjectToInferredTypesConverter());
            opts.Converters.Add(new JsonStringEnumConverter());

            Options = opts;
        }
    }
}
