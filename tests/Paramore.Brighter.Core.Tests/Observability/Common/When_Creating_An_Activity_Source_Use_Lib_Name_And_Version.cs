using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Common;

public class BrighterActivitySourceTests 
{
    [Fact]
    public void When_creating_an_activity_source_use_lib_name_and_version()
    {
        //arrange
        var assemblyName = typeof(BrighterTracer).Assembly.GetName();
        var sourceName = assemblyName.Name;
        var version = assemblyName.Version?.ToString();        
        
        //act
        var activitySource = new BrighterTracer().ActivitySource;
        
        //assert
        Assert.Equal(sourceName, activitySource.Name);
        Assert.Equal(version, activitySource.Version);
    }
}
