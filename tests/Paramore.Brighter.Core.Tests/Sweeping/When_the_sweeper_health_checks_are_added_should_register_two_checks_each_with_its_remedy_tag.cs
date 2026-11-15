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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperHealthChecksRegistrationTests
{
    [Test]
    public async Task When_the_sweeper_health_checks_are_added_should_register_two_checks_each_with_its_remedy_tag()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        using var provider = services.BuildServiceProvider();

        //Act
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        //Assert
        await Assert.That(registrations.Count).IsEqualTo(2);
        var restart = await Assert.That(System.Linq.Enumerable.Where(registrations, r => r.Name == "brighter-outbox-sweeper-restart")).HasSingleItem();
        await Assert.That(restart.Tags.ToArray()).IsEqualTo(new[] { RemedyCheckConvention.RestartTag });
        var investigate = await Assert.That(System.Linq.Enumerable.Where(registrations, r => r.Name == "brighter-outbox-sweeper-investigate")).HasSingleItem();
        await Assert.That(investigate.Tags.ToArray()).IsEqualTo(new[] { RemedyCheckConvention.InvestigateTag });
    }

    [Test]
    public async Task When_the_sweeper_health_checks_are_not_added_should_register_neither_check()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddHealthChecks();
        using var provider = services.BuildServiceProvider();

        //Act
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        //Assert
        await Assert.That(System.Linq.Enumerable.Any(registrations, r => r.Name == "brighter-outbox-sweeper-restart")).IsFalse();
        await Assert.That(System.Linq.Enumerable.Any(registrations, r => r.Name == "brighter-outbox-sweeper-investigate")).IsFalse();
    }
}
