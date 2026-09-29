using System.Linq;
using Paramore.Brighter.Outbox.Hosting;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Sweeping;

public class OutboxHostingAssemblyReferencesTests
{
    [Test]
    public async Task When_the_outbox_hosting_assembly_is_loaded_should_reference_no_aspnetcore_opentelemetry_or_consumer_diagnostics()
    {
        //Arrange
        var hostingAssembly = typeof(TimedOutboxSweeper).Assembly;

        //Act
        var referencedNames = hostingAssembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToList();

        //Assert
        await Assert.That(System.Linq.Enumerable.Any(referencedNames, name => name.StartsWith("Microsoft.AspNetCore"))).IsFalse();
        await Assert.That(System.Linq.Enumerable.Any(referencedNames, name => name.StartsWith("OpenTelemetry"))).IsFalse();
        await Assert.That(System.Linq.Enumerable.Any(referencedNames, name => name == "Paramore.Brighter.ServiceActivator.Extensions.Diagnostics")).IsFalse();
    }
}
