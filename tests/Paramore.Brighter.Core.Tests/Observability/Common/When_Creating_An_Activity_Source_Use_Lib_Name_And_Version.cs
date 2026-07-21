using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.Common;
public class BrighterActivitySourceTests
{
    [Test]
    public async Task When_creating_an_activity_source_use_lib_name_and_version()
    {
        //arrange
        var assemblyName = typeof(BrighterTracer).Assembly.GetName();
        var sourceName = assemblyName.Name;
        var version = assemblyName.Version?.ToString();
        //act
        var activitySource = new BrighterTracer().ActivitySource;
        //assert
        await Assert.That(activitySource.Name).IsEqualTo(sourceName);
        await Assert.That(activitySource.Version).IsEqualTo(version);
    }
}