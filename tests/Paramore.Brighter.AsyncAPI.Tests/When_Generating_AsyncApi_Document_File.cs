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
using Neuroglia.AsyncApi.IO;
using Neuroglia.AsyncApi.v3;

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

        private static IHost CreateHostWithGenerator(V3AsyncApiDocument document)
        {
            var generator = A.Fake<IAmAnAsyncApiDocumentGenerator>();
            A.CallTo(() => generator.GenerateAsync(A<CancellationToken>.Ignored))
                .Returns(Task.FromResult(document));

            var services = new ServiceCollection();
            services.AddSingleton(generator);
            services.AddAsyncApiIO();
            var provider = services.BuildServiceProvider();

            var host = A.Fake<IHost>();
            A.CallTo(() => host.Services).Returns(provider);
            return host;
        }

        [Test]
        public async Task It_Should_Write_Json_File()
        {
            var document = new V3AsyncApiDocument
            {
                Info = new V3ApiInfo { Title = "Test", Version = "1.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

            await Assert.That(File.Exists(outputPath)).IsTrue();
        }

        [Test]
        public async Task It_Should_Write_Yaml_File_Alongside_Json()
        {
            var document = new V3AsyncApiDocument
            {
                Info = new V3ApiInfo { Title = "Test", Version = "1.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

            var yamlPath = Path.Combine(_tempDir, "asyncapi.yaml");
            await Assert.That(File.Exists(yamlPath)).IsTrue();
        }

        [Test]
        public async Task It_Should_Write_Valid_Yaml_Content()
        {
            var document = new V3AsyncApiDocument
            {
                Info = new V3ApiInfo { Title = "YAML Test", Version = "2.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

            var yamlPath = Path.Combine(_tempDir, "asyncapi.yaml");
            var yaml = File.ReadAllText(yamlPath);
            await Assert.That(yaml).Contains("asyncapi:");
            await Assert.That(yaml).Contains("YAML Test");
            await Assert.That(yaml).Contains("2.0.0");
        }

        [Test]
        public async Task It_Should_Create_Parent_Directory()
        {
            var document = new V3AsyncApiDocument
            {
                Info = new V3ApiInfo { Title = "Test", Version = "1.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var nestedDir = Path.Combine(_tempDir, "nested", "dir");
            var outputPath = Path.Combine(nestedDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

            await Assert.That(Directory.Exists(nestedDir)).IsTrue();
            await Assert.That(File.Exists(outputPath)).IsTrue();
            await Assert.That(File.Exists(Path.Combine(nestedDir, "asyncapi.yaml"))).IsTrue();
        }

        [Test]
        public async Task It_Should_Return_Generated_Document()
        {
            var document = new V3AsyncApiDocument
            {
                Info = new V3ApiInfo { Title = "My API", Version = "2.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            var result = await host.GenerateAsyncApiDocumentAsync(outputPath);

            await Assert.That(result.Info.Title).IsEqualTo("My API");
            await Assert.That(result.Info.Version).IsEqualTo("2.0.0");
        }

        [Test]
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

        [Test]
        public async Task It_Should_Write_Valid_Json()
        {
            var document = new V3AsyncApiDocument
            {
                Info = new V3ApiInfo { Title = "Test", Version = "1.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            await host.GenerateAsyncApiDocumentAsync(outputPath);

            var json = File.ReadAllText(outputPath);

            // Verify valid JSON
            using var parsed = JsonDocument.Parse(json);
            await Assert.That(parsed.RootElement.TryGetProperty("asyncapi", out _)).IsTrue();
        }

        [Test]
        public async Task It_Should_Produce_File_Matching_Returned_Document()
        {
            var document = new V3AsyncApiDocument
            {
                Info = new V3ApiInfo { Title = "Matching Test", Version = "3.0.0" }
            };
            var host = CreateHostWithGenerator(document);
            var outputPath = Path.Combine(_tempDir, "asyncapi.json");

            var result = await host.GenerateAsyncApiDocumentAsync(outputPath);

            var json = File.ReadAllText(outputPath);
            using var parsed = JsonDocument.Parse(json);
            await Assert.That(parsed.RootElement.GetProperty("info").GetProperty("title").GetString()).IsEqualTo(result.Info.Title);
            await Assert.That(parsed.RootElement.GetProperty("info").GetProperty("version").GetString()).IsEqualTo(result.Info.Version);
        }
    }
}