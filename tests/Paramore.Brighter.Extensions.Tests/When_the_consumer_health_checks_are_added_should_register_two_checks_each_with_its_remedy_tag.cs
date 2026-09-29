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



#nullable enable

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using System.Threading.Tasks;
namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Verifies the opt-in call registers the consumer remedy checks, one remedy tag each.
/// </summary>
public class ConsumerHealthChecksRegistrationTests
{
    [Test]
    public async Task When_the_consumer_health_checks_are_added_should_register_two_checks_each_with_its_remedy_tag()
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        //Act
        builder.AddBrighterConsumerHealthChecks();
        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        //Assert
        var takeOutOfService = await Assert.That(System.Linq.Enumerable.Where(registrations, registration => registration.Name == "brighter-consumer-take-out-of-service")).HasSingleItem();
        await Assert.That(takeOutOfService.Tags.ToArray()).IsEqualTo(new[] { "brighter-take-out-of-service" });
        await Assert.That(takeOutOfService.FailureStatus).IsEqualTo(HealthStatus.Degraded);
        var restart = await Assert.That(System.Linq.Enumerable.Where(registrations, registration => registration.Name == "brighter-consumer-restart")).HasSingleItem();
        await Assert.That(restart.Tags.ToArray()).IsEqualTo(new[] { "brighter-restart" });
        await Assert.That(restart.FailureStatus).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(registrations.Count).IsEqualTo(2);
    }

    [Test]
    public async Task When_the_consumer_health_checks_are_not_added_should_register_neither_check()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddHealthChecks();
        services.AddOptions();

        //Act
        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        //Assert
        await Assert.That(System.Linq.Enumerable.Any(registrations, registration => registration.Name == "brighter-consumer-take-out-of-service")).IsFalse();
        await Assert.That(System.Linq.Enumerable.Any(registrations, registration => registration.Name == "brighter-consumer-restart")).IsFalse();
    }
}
