using System.Linq;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class OutboxHostingAssemblyReferencesTests
{
    [Fact]
    public void When_the_outbox_hosting_assembly_is_loaded_should_reference_no_aspnetcore_opentelemetry_or_consumer_diagnostics()
    {
        //Arrange
        var hostingAssembly = typeof(TimedOutboxSweeper).Assembly;

        //Act
        var referencedNames = hostingAssembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToList();

        //Assert
        Assert.DoesNotContain(referencedNames, name => name.StartsWith("Microsoft.AspNetCore"));
        Assert.DoesNotContain(referencedNames, name => name.StartsWith("OpenTelemetry"));
        Assert.DoesNotContain(referencedNames, name => name == "Paramore.Brighter.ServiceActivator.Extensions.Diagnostics");
    }
}
