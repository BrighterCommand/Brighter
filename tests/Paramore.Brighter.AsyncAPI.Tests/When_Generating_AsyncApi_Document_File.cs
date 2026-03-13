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

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
            A.CallTo(() => generator.GenerateAsync(A<CancellationToken>.Ignored))
                .Returns(Task.FromResult(document));

            var services = new ServiceCollection();
            services.AddSingleton(generator);
            var provider = services.BuildServiceProvider();

            var host = A.Fake<IHost>();
            A.CallTo(() => host.Services).Returns(provider);
            return host;
        }

        [Fact]
        public async Task It_Should_Write_Json_File()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "Test", Version = "1.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

            Assert.True(File.Exists(outputPath));
        }

        [Fact]
        public async Task It_Should_Write_Yaml_File_Alongside_Json()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "Test", Version = "1.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

            var yamlPath = Path.Combine(_tempDir, "asyncapi.yaml");
            Assert.True(File.Exists(yamlPath));
        }

        [Fact]
        public async Task It_Should_Write_Valid_Yaml_Content()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "YAML Test", Version = "2.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

            var yamlPath = Path.Combine(_tempDir, "asyncapi.yaml");
            var yaml = File.ReadAllText(yamlPath);
            Assert.Contains("asyncapi: 3.0.0", yaml);
            Assert.Contains("title: YAML Test", yaml);
            Assert.Contains("version: 2.0.0", yaml);
        }

        [Fact]
        public async Task It_Should_Create_Parent_Directory()
        {
            var document = new AsyncApiDocument();
            var host = CreateHostWithGenerator(document);
            var nestedDir = Path.Combine(_tempDir, "nested", "dir");
            var outputPath = Path.Combine(nestedDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

            Assert.True(Directory.Exists(nestedDir));
            Assert.True(File.Exists(outputPath));
            Assert.True(File.Exists(Path.Combine(nestedDir, "asyncapi.yaml")));
        }

        [Fact]
        public async Task It_Should_Return_Generated_Document()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "My API", Version = "2.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            var result = await host.GenerateAsyncApiDocumentAsync(outputPath);

            Assert.Equal("My API", result.Info.Title);
            Assert.Equal("2.0.0", result.Info.Version);
        }

        [Fact]
        public async Task It_Should_Throw_When_Not_Configured()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var host = A.Fake<IHost>();
            A.CallTo(() => host.Services).Returns(provider);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await host.GenerateAsyncApiDocumentAsync(outputPath));
        }

        [Fact]
        public async Task It_Should_Write_Indented_Json_Without_Nulls()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "Test", Version = "1.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

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
        public async Task It_Should_Produce_File_Matching_Returned_Document()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "Matching Test", Version = "3.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            var result = await host.GenerateAsyncApiDocumentAsync(outputPath);

            var json = File.ReadAllText(outputPath);
            using var parsed = JsonDocument.Parse(json);
            Assert.Equal(result.Info.Title, parsed.RootElement.GetProperty("info").GetProperty("title").GetString());
            Assert.Equal(result.Info.Version, parsed.RootElement.GetProperty("info").GetProperty("version").GetString());
        }

        [Fact]
        public async Task It_Should_Omit_Nulls_From_Yaml()
        {
            var document = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "Null Test", Version = "1.0.0", Description = null }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

            var yaml = File.ReadAllText(Path.Combine(_tempDir, "asyncapi.yaml"));
            Assert.DoesNotContain("description", yaml);
        }
    }
}
