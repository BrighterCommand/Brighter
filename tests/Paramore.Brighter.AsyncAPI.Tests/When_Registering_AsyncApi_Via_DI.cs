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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Neuroglia.AsyncApi.v3;
using Paramore.Brighter.AsyncAPI.NJsonSchema;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.AsyncAPI.Tests
{
    public class When_Registering_AsyncApi_Via_DI
    {
        private static IBrighterBuilder CreateFakeBuilder(ServiceCollection services)
        {
            var builder = A.Fake<IBrighterBuilder>();
            A.CallTo(() => builder.Services).Returns(services);
            return builder;
        }

        [Test]
        public async Task It_Should_Register_Options_As_Singleton()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi(opts => opts.Title = "My API");

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<AsyncApiOptions>();
            await Assert.That(options.Title).IsEqualTo("My API");
        }

        [Test]
        public async Task It_Should_Register_Generator()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi();

            var provider = services.BuildServiceProvider();
            var generator = provider.GetRequiredService<IAmAnAsyncApiDocumentGenerator>();
            await Assert.That(generator).IsNotNull();
        }

        [Test]
        public async Task It_Should_Register_Default_Schema_Generator()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi();

            var provider = services.BuildServiceProvider();
            var schemaGen = provider.GetRequiredService<IAmASchemaGenerator>();
            await Assert.That(schemaGen).IsTypeOf<NJsonSchemaGenerator>();
        }

        [Test]
        public async Task It_Should_Not_Override_Custom_Schema_Generator()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IAmASchemaGenerator>(new FakeSchemaGenerator());
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi();

            var provider = services.BuildServiceProvider();
            var schemaGen = provider.GetRequiredService<IAmASchemaGenerator>();
            await Assert.That(schemaGen).IsTypeOf<FakeSchemaGenerator>();
        }

        [Test]
        public async Task It_Should_Apply_Configure_Delegate()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi(opts =>
            {
                opts.Title = "Custom Title";
                opts.Version = "2.0.0";
                opts.Description = "Custom description";
            });

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<AsyncApiOptions>();
            await Assert.That(options.Title).IsEqualTo("Custom Title");
            await Assert.That(options.Version).IsEqualTo("2.0.0");
            await Assert.That(options.Description).IsEqualTo("Custom description");
        }

        [Test]
        public async Task It_Should_Return_Builder_For_Chaining()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            var result = builder.UseAsyncApi();

            await Assert.That(result).IsSameReferenceAs(builder);
        }

        [Test]
        public async Task It_Should_Generate_Document_With_Default_Options()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi(opts => opts.DisableAssemblyScanning = true);

            var provider = services.BuildServiceProvider();
            var generator = provider.GetRequiredService<IAmAnAsyncApiDocumentGenerator>();
            var doc = await generator.GenerateAsync();

            await Assert.That(doc.AsyncApi).IsEqualTo("3.0.0");
            await Assert.That(doc.Info.Title).IsEqualTo("Brighter Application");
            await Assert.That(doc.Info.Version).IsEqualTo("1.0.0");
        }

        private sealed class FakeSchemaGenerator : IAmASchemaGenerator
        {
            public Task<V3SchemaDefinition?> GenerateAsync(Type? requestType, CancellationToken ct = default)
            {
                using var doc = JsonDocument.Parse("{}");
                return Task.FromResult<V3SchemaDefinition?>(new V3SchemaDefinition
                {
                    SchemaFormat = "application/schema+json;version=draft-07",
                    Schema = doc.RootElement.Clone()
                });
            }
        }
    }
}