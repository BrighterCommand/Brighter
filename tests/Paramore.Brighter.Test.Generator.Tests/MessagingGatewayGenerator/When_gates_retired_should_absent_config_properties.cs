using System.Reflection;
using Paramore.Brighter.Test.Generator.Configuration;


namespace Paramore.Brighter.Test.Generator.Tests.MessagingGatewayGenerator;

/// <summary>
/// Gate test for Phase 5 step 3 of the ADR 0066 "Step C" cleanup.
///
/// Asserts:
///   1. <see cref="MessagingGatewayConfiguration"/> has no <c>HasSupportToDelayedMessages</c>,
///      <c>HasSupportToDeadLetterQueue</c>, or <c>HasSupportToRequeue</c> members — the three
///      retired capability-gate properties.
///   2. The four retained flag properties remain present — confirming the type was not emptied
///      or renamed, and that the cleanup was surgical.
/// </summary>
public class WhenGatesRetiredShouldAbsentConfigProperties
{
    [Test]
    public async System.Threading.Tasks.Task When_gates_retired_should_absent_config_properties()
    {
        // Arrange
        var type = typeof(MessagingGatewayConfiguration);

        // Act + Assert — the three retired properties are absent from the compiled surface
        await Assert.That(type.GetProperty("HasSupportToDelayedMessages", BindingFlags.Public | BindingFlags.Instance) == null).IsTrue().Because("HasSupportToDelayedMessages must be absent — its gate was retired (ADR 0066 Step C, FR-10(4))");

        await Assert.That(type.GetProperty("HasSupportToDeadLetterQueue", BindingFlags.Public | BindingFlags.Instance) == null).IsTrue().Because("HasSupportToDeadLetterQueue must be absent — its gate was retired (ADR 0066 Step C, FR-10(4))");

        await Assert.That(type.GetProperty("HasSupportToRequeue", BindingFlags.Public | BindingFlags.Instance) == null).IsTrue().Because("HasSupportToRequeue must be absent — its gate was retired (ADR 0066 Step C, FR-10(4))");

        // Assert — the retained flag properties are still present (not emptied or renamed)
        await Assert.That(type.GetProperty("HasSupportToPublishConfirmation", BindingFlags.Public | BindingFlags.Instance) != null).IsTrue().Because("HasSupportToPublishConfirmation must remain — it gates a retained template (confirming_posting)");

        await Assert.That(type.GetProperty("HasSupportToValidateBrokerExistence", BindingFlags.Public | BindingFlags.Instance) != null).IsTrue().Because("HasSupportToValidateBrokerExistence must remain — it gates a retained template (no_broker_created)");

        await Assert.That(type.GetProperty("HasSupportToValidateInfrastructure", BindingFlags.Public | BindingFlags.Instance) != null).IsTrue().Because("HasSupportToValidateInfrastructure must remain — it gates retained templates (assume_channel, validate_channel)");
    }
}
