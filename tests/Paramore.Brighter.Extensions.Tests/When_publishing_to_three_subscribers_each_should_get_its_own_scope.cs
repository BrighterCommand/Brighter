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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// AC-10 (FR-8, D10, ADR 0039 regression guard) — an FR-22.2-conformant lifetime triple
// {Scoped, Scoped, Scoped}, not opted in, three handlers registered for OrderPlaced each taking a
// Scoped IUnitOfWork. Each subscriber must own its own IServiceScope: an implementation that resolved
// all three from one shared scope would hand them the same IUnitOfWork instance, and one that resolved
// from the root provider would leave the instances undisposed after PublishAsync returns.
public class PublishSubscriberScopeTeardownTests
{
    [Fact]
    public async Task When_publishing_to_three_subscribers_each_should_get_its_own_scope()
    {
        // Arrange
        var recorder = new UnitOfWorkRecorder();
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(recorder);
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        var commandProcessor = services.BuildServiceProvider().GetRequiredService<IAmACommandProcessor>();

        // Act
        await commandProcessor.PublishAsync(new OrderPlaced());

        // Assert — three distinct IUnitOfWork instances were resolved, one per subscriber
        Assert.Equal(3, recorder.UnitsOfWork.Count);
        Assert.Equal(3, recorder.UnitsOfWork.Distinct().Count());

        // Assert — by the time PublishAsync returns, all three have already been disposed
        Assert.All(recorder.UnitsOfWork, unitOfWork => Assert.True(unitOfWork.IsDisposed));
    }
}
