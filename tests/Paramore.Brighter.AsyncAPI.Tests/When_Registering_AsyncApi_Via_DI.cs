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
using Paramore.Brighter.AsyncAPI.NJsonSchema;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

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

        [Fact]
        public void It_Should_Register_Options_As_Singleton()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi(opts => opts.Title = "My API");

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<AsyncApiOptions>();
            Assert.Equal("My API", options.Title);
        }

        [Fact]
        public void It_Should_Register_Generator()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi();

            var provider = services.BuildServiceProvider();
            var generator = provider.GetRequiredService<IAmAnAsyncApiDocumentGenerator>();
            Assert.NotNull(generator);
        }

        [Fact]
        public void It_Should_Register_Default_Schema_Generator()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi();

            var provider = services.BuildServiceProvider();
            var schemaGen = provider.GetRequiredService<IAmASchemaGenerator>();
            Assert.IsType<NJsonSchemaGenerator>(schemaGen);
        }

        [Fact]
        public void It_Should_Not_Override_Custom_Schema_Generator()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IAmASchemaGenerator>(new FakeSchemaGenerator());
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi();

            var provider = services.BuildServiceProvider();
            var schemaGen = provider.GetRequiredService<IAmASchemaGenerator>();
            Assert.IsType<FakeSchemaGenerator>(schemaGen);
        }

        [Fact]
        public void It_Should_Apply_Configure_Delegate()
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
            Assert.Equal("Custom Title", options.Title);
            Assert.Equal("2.0.0", options.Version);
            Assert.Equal("Custom description", options.Description);
        }

        [Fact]
        public void It_Should_Return_Builder_For_Chaining()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            var result = builder.UseAsyncApi();

            Assert.Same(builder, result);
        }

        [Fact]
        public async Task It_Should_Generate_Document_With_Default_Options()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var builder = CreateFakeBuilder(services);

            builder.UseAsyncApi(opts => opts.DisableAssemblyScanning = true);

            var provider = services.BuildServiceProvider();
            var generator = provider.GetRequiredService<IAmAnAsyncApiDocumentGenerator>();
            var doc = await generator.GenerateAsync();

            Assert.Equal("3.0.0", doc.AsyncApi);
            Assert.Equal("Brighter Application", doc.Info.Title);
            Assert.Equal("1.0.0", doc.Info.Version);
        }

        private sealed class FakeSchemaGenerator : IAmASchemaGenerator
        {
            public Task<JsonElement?> GenerateAsync(Type? requestType, CancellationToken ct = default)
            {
                using var doc = JsonDocument.Parse("{}");
                return Task.FromResult<JsonElement?>(doc.RootElement.Clone());
            }
        }
    }
}
