namespace Paramore.Brighter.Core.Tests.NamingConventions;
public class DeadLetterNamingConventionCustomTemplateTests
{
    [Test]
    public async Task When_creating_dead_letter_name_with_custom_template_should_use_template()
    {
        //Arrange
        var customTemplate = "failed-{0}";
        var convention = new DeadLetterNamingConvention(customTemplate);
        var dataTopic = new RoutingKey("orders");
        //Act
        var deadLetterRoutingKey = convention.MakeChannelName(dataTopic);
        //Assert
        await Assert.That(deadLetterRoutingKey.Value).IsEqualTo("failed-orders");
    }
}