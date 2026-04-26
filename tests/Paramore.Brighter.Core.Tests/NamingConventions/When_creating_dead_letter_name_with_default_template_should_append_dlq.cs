namespace Paramore.Brighter.Core.Tests.NamingConventions;
public class DeadLetterNamingConventionDefaultTemplateTests
{
    [Test]
    public async Task When_creating_dead_letter_name_with_default_template_should_append_dlq()
    {
        //Arrange
        var convention = new DeadLetterNamingConvention();
        var dataTopic = new RoutingKey("orders");
        //Act
        var deadLetterRoutingKey = convention.MakeChannelName(dataTopic);
        //Assert
        await Assert.That(deadLetterRoutingKey.Value).IsEqualTo("orders.dlq");
    }
}