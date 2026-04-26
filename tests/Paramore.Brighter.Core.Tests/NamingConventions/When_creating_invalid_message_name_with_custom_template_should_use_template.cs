namespace Paramore.Brighter.Core.Tests.NamingConventions;
public class InvalidMessageNamingConventionCustomTemplateTests
{
    [Test]
    public async Task When_creating_invalid_message_name_with_custom_template_should_use_template()
    {
        //Arrange
        var customTemplate = "bad-{0}";
        var convention = new InvalidMessageNamingConvention(customTemplate);
        var dataTopic = new RoutingKey("orders");
        //Act
        var invalidMessageRoutingKey = convention.MakeChannelName(dataTopic);
        //Assert
        await Assert.That(invalidMessageRoutingKey.Value).IsEqualTo("bad-orders");
    }
}