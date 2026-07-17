using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.Trace;
public class TraceStateTests
{
    [Test]
    public async Task When_Adding_TraceState()
    {
        // Arrange
        var traceState = new Baggage();
        //act
        traceState.Add("key1", "value1");
        traceState.Add("key2", "value2");
        // Assert
        await Assert.That(traceState.ToString()).IsEqualTo("key1=value1,key2=value2");
    }

    [Test]
    public async Task When_Adding_TraceState_From_Baggage()
    {
        //Arrange
        var traceState = new Baggage();
        var baggage = "key1=value1,key2=value2";
        //Act
        traceState.Add("key3", "value3");
        traceState.LoadBaggage(baggage);
        //Assert
        await Assert.That(traceState.ToString()).IsEqualTo("key3=value3,key1=value1,key2=value2");
    }

    [Test]
    public async Task When_Enumerating_TraceState()
    {
        //Arrange
        var traceState = new Baggage();
        traceState.Add("key1", "value1");
        traceState.Add("key2", "value2");
        //Act
        //Assert
        var entries = traceState.ToList();
        await Assert.That(entries).Count().IsEqualTo(2);
        await Assert.That(entries[0].Key).IsEqualTo("key1");
        await Assert.That(entries[0].Value).IsEqualTo("value1");
        await Assert.That(entries[1].Key).IsEqualTo("key2");
        await Assert.That(entries[1].Value).IsEqualTo("value2");
    }
}