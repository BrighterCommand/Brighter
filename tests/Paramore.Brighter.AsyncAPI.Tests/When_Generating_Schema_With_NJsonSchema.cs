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

using System.Text.Json;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.AsyncAPI.NJsonSchema;
using Xunit;

namespace Paramore.Brighter.AsyncAPI.Tests
{
    public class When_Generating_Schema_With_NJsonSchema
    {
        private readonly NJsonSchemaGenerator _generator;

        public When_Generating_Schema_With_NJsonSchema()
        {
            var logger = A.Fake<ILogger<NJsonSchemaGenerator>>();
            _generator = new NJsonSchemaGenerator(logger);
        }

        [Fact]
        public async Task It_Should_Return_Empty_Object_For_Null_Type()
        {
            var result = await _generator.GenerateAsync(null);

            Assert.NotNull(result);
            Assert.Equal(JsonValueKind.Object, result.Value.ValueKind);
            Assert.Equal("{}", result.Value.GetRawText());
        }

        [Fact]
        public async Task It_Should_Generate_Schema_For_Valid_Type()
        {
            var result = await _generator.GenerateAsync(typeof(TestSchemaType));

            Assert.NotNull(result);
            Assert.Equal(JsonValueKind.Object, result.Value.ValueKind);
            Assert.True(result.Value.TryGetProperty("properties", out var properties));
            Assert.True(properties.TryGetProperty("Name", out _));
            Assert.True(properties.TryGetProperty("Age", out _));
        }

        private sealed class TestSchemaType
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }
    }
}
