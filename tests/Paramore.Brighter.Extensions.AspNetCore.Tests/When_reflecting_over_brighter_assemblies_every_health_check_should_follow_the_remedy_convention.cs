#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#pragma warning disable CS0618

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

/// <summary>
/// Verifies every public Brighter health check is exempt or registered by a named call with only its remedy tag.
/// </summary>
public class RemedyConventionReflectionTests
{
    private static readonly string[] s_remedyTags =
    {
        RemedyCheckConvention.RestartTag,
        RemedyCheckConvention.TakeOutOfServiceTag,
        RemedyCheckConvention.InvestigateTag
    };

    [Fact]
    public void When_reflecting_over_brighter_assemblies_every_health_check_should_follow_the_remedy_convention()
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();
        builder.AddBrighterOutboxSweeperHealthChecks();
        builder.AddBrighterConsumerHealthChecks();
        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        var exempt = new[] { typeof(BrighterServiceActivatorHealthCheck) };

        //Act
        var registeredTypes = registrations.Select(registration => registration.Factory(provider).GetType()).ToArray();
        var publicHealthChecks = PublicHealthCheckTypesInBrighterAssemblies().Except(exempt).ToArray();

        //Assert
        Assert.Equal(
            publicHealthChecks.Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal),
            registeredTypes.Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(registrations, registration =>
        {
            var tag = Assert.Single(registration.Tags);
            Assert.Contains(tag, s_remedyTags);
        });
    }

    private static Type[] PublicHealthCheckTypesInBrighterAssemblies()
    {
        return Directory.GetFiles(AppContext.BaseDirectory, "Paramore.Brighter*.dll")
            .Select(Assembly.LoadFrom)
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IHealthCheck).IsAssignableFrom(type))
            .Distinct()
            .ToArray();
    }
}
