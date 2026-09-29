using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.Trace;

public class NamespacedBaggageTests
{
    [Test]
    [Arguments("tunit.test.id")]
    [Arguments("tenant.region")]
    public async Task When_baggage_contains_namespaced_keys_should_round_trip(string key)
    {
        //Arrange
        var serialized = $"{key}=value";

        //Act
        var baggage = Baggage.FromString(serialized);

        //Assert
        await Assert.That(baggage.ToString()).IsEqualTo(serialized);
    }
}
