#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.IO;
using System.Text.Json;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.AsyncAPI.Model;
using Xunit;

namespace Paramore.Brighter.AsyncAPI.Tests
{
    public class When_Generating_AsyncApi_Document_File : IDisposable
    {
        private readonly string _tempDir;

        public When_Generating_AsyncApi_Document_File()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private static IHost CreateHostWithGenerator(AsyncApiDocument document)
        {
            var generator = A.Fake<IAmAnAsyncApiDocumentGenerator>();
            A.CallTo(() => generator.Generate()).Returns(document);

            var services = new ServiceCollection();
            services.AddSingleton(generator);
            var provider = services.BuildServiceProvider();

            var host = A.Fake<IHost>();
            A.CallTo(() => host.Services).Returns(provider);
            return host;
        }

        [Fact]
        public void It_Should_Write_File()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "Test", Version = "1.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            host.GenerateAsyncApiDocument(outputPath);

            Assert.True(File.Exists(outputPath));
        }

        [Fact]
        public void It_Should_Create_Parent_Directory()
        {
            var document = new AsyncApiDocument();
            var host = CreateHostWithGenerator(document);
            var nestedDir = Path.Combine(_tempDir, "nested", "dir");
            var outputPath = Path.Combine(nestedDir, "asyncapi.json");

            host.GenerateAsyncApiDocument(outputPath);

            Assert.True(Directory.Exists(nestedDir));
            Assert.True(File.Exists(outputPath));
        }

        [Fact]
        public void It_Should_Return_Generated_Document()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "My API", Version = "2.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            var result = host.GenerateAsyncApiDocument(outputPath);

            Assert.Equal("My API", result.Info.Title);
            Assert.Equal("2.0.0", result.Info.Version);
        }

        [Fact]
        public void It_Should_Throw_When_Not_Configured()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var host = A.Fake<IHost>();
            A.CallTo(() => host.Services).Returns(provider);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                host.GenerateAsyncApiDocument(outputPath));

            Assert.Contains("UseAsyncApi()", ex.Message);
        }

        [Fact]
        public void It_Should_Write_Indented_Json_Without_Nulls()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "Test", Version = "1.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            host.GenerateAsyncApiDocument(outputPath);

            var json = File.ReadAllText(outputPath);

            // Should be indented (contains newlines)
            Assert.Contains("\n", json);
            // Should not contain null properties
            Assert.DoesNotContain("null", json);
            // Verify valid JSON
            using var parsed = JsonDocument.Parse(json);
            Assert.Equal("3.0.0", parsed.RootElement.GetProperty("asyncapi").GetString());
        }

        [Fact]
        public void It_Should_Produce_File_Matching_Returned_Document()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "Matching Test", Version = "3.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            var result = host.GenerateAsyncApiDocument(outputPath);

            var json = File.ReadAllText(outputPath);
            using var parsed = JsonDocument.Parse(json);
            Assert.Equal(result.Info.Title, parsed.RootElement.GetProperty("info").GetProperty("title").GetString());
            Assert.Equal(result.Info.Version, parsed.RootElement.GetProperty("info").GetProperty("version").GetString());
        }
    }
}
