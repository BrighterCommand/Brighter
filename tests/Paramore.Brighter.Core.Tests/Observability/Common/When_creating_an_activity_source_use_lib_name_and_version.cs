using FluentAssertions;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Common;

public class BrighterActivitySourceTests 
{
    [Fact]
    public void When_creating_an_activity_source_use_lib_name_and_version()
    {
        //act
        var activitySource = BrighterTracer.ActivitySource;
        
        //assert
        BrighterTracer.SourceName.Should().Be("Paramore.Brighter");
        BrighterTracer.Version.Should().NotBeNull();
        activitySource.Name.Should().Be(BrighterTracer.SourceName);
        activitySource.Version.Should().Be(BrighterTracer.Version);
    }
}
