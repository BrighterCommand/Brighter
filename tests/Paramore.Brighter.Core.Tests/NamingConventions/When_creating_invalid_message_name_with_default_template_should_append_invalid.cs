namespace Paramore.Brighter.Core.Tests.NamingConventions;
public class InvalidMessageNamingConventionDefaultTemplateTests
{
    [Test]
    public async Task When_creating_invalid_message_name_with_default_template_should_append_invalid()
    {
        //Arrange
        var convention = new InvalidMessageNamingConvention();
        var dataTopic = new RoutingKey("orders");
        //Act
        var invalidMessageRoutingKey = convention.MakeChannelName(dataTopic);
        //Assert
        await Assert.That(invalidMessageRoutingKey.Value).IsEqualTo("orders.invalid");
    }
}