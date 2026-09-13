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

using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// The ambient-adoption seam must not be an ASP.NET-only feature: any host that owns some notion of a
// current unit-of-work scope - a console application flowing one through an AsyncLocal, with no
// reference to any web hosting package - can offer it to Brighter and have a Send resolve its Scoped
// dependencies from that scope instead of one Brighter creates. When no such scope is established, a
// Send must fall back to creating and owning its own, exactly as it does when nothing is registered.
public class NonAspNetAmbientAdoptionTests
{
    [Fact]
    public void When_a_non_aspnet_provider_offers_an_ambient_the_pipeline_should_adopt_it()
    {
        // Arrange - a host with no ASP.NET reference, opted in to joining an ambient scope, whose
        // handler resolves a Scoped IUnitOfWork
        var recorder = new UnitOfWorkRecorder();
        var scopeProvider = new AsyncLocalScopeProvider();
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(recorder);
        services.AddSingleton<IAmAScopeProvider>(scopeProvider);
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
            options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
        });
        var rootProvider = services.BuildServiceProvider();
        var commandProcessor = rootProvider.GetRequiredService<IAmACommandProcessor>();

        // Act - a Send made with an ambient scope established
        IUnitOfWork ambientUnitOfWork;
        using (var ambientScope = rootProvider.CreateScope())
        {
            ambientUnitOfWork = ambientScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            scopeProvider.Establish(new AsyncLocalAmbientScope(ambientScope.ServiceProvider));

            commandProcessor.Send(new AmbientAdoptionCommand());

            scopeProvider.Clear();

            // Assert - the handler resolved the ambient's own instance, and Brighter did not dispose it
            var resolvedWithinAmbient = Assert.Single(recorder.UnitsOfWork);
            Assert.Same(ambientUnitOfWork, resolvedWithinAmbient);
            Assert.False(ambientUnitOfWork.IsDisposed);
        }

        // Act - a second Send made outside any established ambient
        commandProcessor.Send(new AmbientAdoptionCommand());

        // Assert - Brighter created and disposed its own scope for this one, distinct from the ambient
        Assert.Equal(2, recorder.UnitsOfWork.Count);
        var resolvedOutsideAmbient = recorder.UnitsOfWork[1];
        Assert.NotSame(ambientUnitOfWork, resolvedOutsideAmbient);
        Assert.True(resolvedOutsideAmbient.IsDisposed);
    }
}
