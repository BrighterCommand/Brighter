using System.Linq;
using System.Reflection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ConsumerDiagnosticsAssemblyReferencesTests
{
    [Fact]
    public void When_the_consumer_diagnostics_assembly_is_loaded_should_reference_no_aspnetcore_or_opentelemetry()
    {
        //Arrange
        var diagnosticsAssembly = Assembly.Load("Paramore.Brighter.ServiceActivator.Extensions.Diagnostics");

        //Act
        var referencedNames = diagnosticsAssembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToList();

        //Assert
        Assert.DoesNotContain(referencedNames, name => name.StartsWith("Microsoft.AspNetCore"));
        Assert.DoesNotContain(referencedNames, name => name.StartsWith("OpenTelemetry"));
    }
}
