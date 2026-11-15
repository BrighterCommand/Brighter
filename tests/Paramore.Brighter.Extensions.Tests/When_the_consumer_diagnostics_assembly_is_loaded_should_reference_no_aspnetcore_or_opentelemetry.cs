using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
namespace Paramore.Brighter.Extensions.Tests;

public class ConsumerDiagnosticsAssemblyReferencesTests
{
    [Test]
    public async Task When_the_consumer_diagnostics_assembly_is_loaded_should_reference_no_aspnetcore_or_opentelemetry()
    {
        //Arrange
        var diagnosticsAssembly = System.Reflection.Assembly.Load("Paramore.Brighter.ServiceActivator.Extensions.Diagnostics");

        //Act
        var referencedNames = diagnosticsAssembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToList();

        //Assert
        await Assert.That(System.Linq.Enumerable.Any(referencedNames, name => name.StartsWith("Microsoft.AspNetCore"))).IsFalse();
        await Assert.That(System.Linq.Enumerable.Any(referencedNames, name => name.StartsWith("OpenTelemetry"))).IsFalse();
    }
}
