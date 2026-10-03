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

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class PipelineScopeDoubleDisposeTests
{
    private readonly ServiceProviderMapperFactory _factory;
    private readonly IAmAScope _scopeX;
    private readonly IAmAScope _scopeY;
    private readonly IMarker _markerY;

    public PipelineScopeDoubleDisposeTests()
    {
        //arrange — an FR-22.2-conformant lifetime triple: all three Scoped, so both pipelines take a
        //pipeline scope (FR-27.1). Two concurrently live pipelines, X and Y, each holding a
        //Brighter-created IAmAScope and each resolving its own Scoped IMarker through it
        var markerLog = new MarkerLog();
        var collection = new ServiceCollection();
        collection.AddSingleton(markerLog);
        collection.AddScoped<IMarker, Marker>();
        collection.AddScoped<MarkerMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var rootProvider = collection.BuildServiceProvider();

        _factory = new ServiceProviderMapperFactory(rootProvider);
        _scopeX = _factory.CreatePipelineScope()!;
        _scopeY = _factory.CreatePipelineScope()!;

        _factory.Create(typeof(MarkerMapper), _scopeX);
        var mapperY = (MarkerMapper)_factory.Create(typeof(MarkerMapper), _scopeY)!.Instance;
        _markerY = mapperY.Marker;
    }

    [Fact]
    public void When_a_pipeline_scopes_dispose_is_called_twice_it_should_not_throw_or_affect_another_pipeline()
    {
        //act — X's scope already disposed once, then Dispose() invoked a second time
        _scopeX.Dispose();
        var exception = Record.Exception(() => _scopeX.Dispose());

        //assert — the second Dispose() raises nothing, and Y's scope stays live and usable
        Assert.Null(exception);
        AssertYRemainsUsable();
    }

    [Fact]
    public async Task When_a_pipeline_scopes_disposeasync_is_called_twice_it_should_not_throw_or_affect_another_pipeline()
    {
        //act — X's scope already disposed once, then DisposeAsync() invoked a second time
        await _scopeX.DisposeAsync();
        var exception = await Record.ExceptionAsync(async () => await _scopeX.DisposeAsync());

        //assert — the second DisposeAsync() raises nothing, and Y's scope stays live and usable
        Assert.Null(exception);
        AssertYRemainsUsable();
    }

    //Y's scope and the Scoped instance already resolved through it remain undisposed, and a further
    //resolution through Y still returns that same live instance — X's double dispose has not reached it
    private void AssertYRemainsUsable()
    {
        Assert.False(_markerY.IsDisposed);
        var mapperYAgain = (MarkerMapper)_factory.Create(typeof(MarkerMapper), _scopeY)!.Instance;
        Assert.Same(_markerY, mapperYAgain.Marker);
        Assert.False(mapperYAgain.Marker.IsDisposed);
    }
}
