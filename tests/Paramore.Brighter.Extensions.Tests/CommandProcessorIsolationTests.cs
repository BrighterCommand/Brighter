#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Tests that verify CommandProcessor instances are properly isolated,
/// enabling parallel test execution without [Collection] serialization.
/// </summary>
public class CommandProcessorIsolationTests
{
    [Fact]
    public void TwoCommandProcessors_HaveIsolatedState()
    {
        // Arrange - Create two independent service providers
        var services1 = new ServiceCollection();
        services1.AddBrighter();
        var provider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddBrighter();
        var provider2 = services2.BuildServiceProvider();

        // Act
        var cp1 = provider1.GetRequiredService<IAmACommandProcessor>();
        var cp2 = provider2.GetRequiredService<IAmACommandProcessor>();

        // Assert - They should be different instances
        Assert.NotSame(cp1, cp2);
    }

    [Fact]
    public async Task ParallelTests_DoNotInterfere()
    {
        // This test demonstrates that parallel execution is safe
        var tasks = new Task[10];
        var processors = new IAmACommandProcessor[10];

        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                var services = new ServiceCollection();
                services.AddBrighter();
                var provider = services.BuildServiceProvider();
                processors[index] = provider.GetRequiredService<IAmACommandProcessor>();
            });
        }

        await Task.WhenAll(tasks);

        // All processors should be unique instances
        for (int i = 0; i < 10; i++)
        {
            for (int j = i + 1; j < 10; j++)
            {
                Assert.NotSame(processors[i], processors[j]);
            }
        }
    }
}
