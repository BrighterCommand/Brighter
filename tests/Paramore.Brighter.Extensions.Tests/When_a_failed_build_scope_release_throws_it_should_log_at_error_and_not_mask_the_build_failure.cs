#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class FailedBuildScopeDisposalLoggingTests
{
    [Fact]
    public void When_a_failed_build_scope_release_throws_it_should_log_at_error_and_not_mask_the_build_failure()
    {
        //arrange — an FR-22.2-conformant lifetime triple: all three Scoped. PoisonedScopeMapper resolves
        //IPoisonedDependency successfully (tracked by the pipeline scope for disposal), then building its
        //[PoisonedWrapWith] transform fails because PoisonedTransform depends on IUnregisteredDependency,
        //which is never registered — a transform resolution failure driving the no-pipeline-constructed
        //branch, after the mapper has already been built into the same scope
        TransformPipelineBuilder.ClearPipelineCache();

        var collection = new ServiceCollection();
        collection.AddScoped<IPoisonedDependency, PoisonedDependency>();
        collection.AddScoped<PoisonedScopeMapper>();
        collection.AddScoped<PoisonedTransform>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var provider = collection.BuildServiceProvider();

        using var mapperFactory = new ServiceProviderMapperFactory(provider);
        using var transformerFactory = new ServiceProviderTransformerFactory(provider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<PoisonedScopeCommand, PoisonedScopeMapper>();

        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, transformerFactory);

        //added to Initializer.Factory directly — the one instance every Brighter static logger in this
        //process is bound to — not to ApplicationLogging.LoggerFactory, which another test may reassign
        var loggerProvider = new CapturingLoggerProvider();
        Initializer.Factory.AddProvider(loggerProvider);

        //act
        Assert.Throws<ConfigurationException>(() => pipelineBuilder.BuildWrapPipeline<PoisonedScopeCommand>());

        //assert — the no-pipeline-constructed branch's own Error message fired once, naming the request type
        var disposalFailures = loggerProvider.Entries
            .Where(e => e.EventId.Name == "FailedToDisposePipelineScopeAfterFailedBuild")
            .ToList();
        var disposalFailure = Assert.Single(disposalFailures);
        Assert.Equal(LogLevel.Error, disposalFailure.Level);
        Assert.Contains(nameof(PoisonedScopeCommand), disposalFailure.Message);

        //assert — the outer cleanup guard did not also log a Warning for the same event
        Assert.DoesNotContain(loggerProvider.Entries, e => e.EventId.Name == "FailedToCleanUpAfterFailedBuild");
    }
}
