using Xunit;

namespace Paramore.Brighter.Core.Tests.NamingConventions;

public class DeadLetterNamingConventionDefaultTemplateTests
{
    [Fact]
    public void When_creating_dead_letter_name_with_default_template_should_append_dlq()
    {
        //Arrange
        var convention = new DeadLetterNamingConvention();
        var dataTopic = new RoutingKey("orders");

        //Act
        var deadLetterRoutingKey = convention.MakeChannelName(dataTopic);

        //Assert
        Assert.Equal("orders.dlq", deadLetterRoutingKey.Value);
    }
}
