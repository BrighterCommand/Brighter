using System.Reflection;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.MessagingGatewayGenerator;

/// <summary>
/// Gate test for Phase 5 step 3 of the ADR 0066 "Step C" cleanup.
///
/// Asserts:
///   1. <see cref="MessagingGatewayConfiguration"/> has no <c>HasSupportToDelayedMessages</c>,
///      <c>HasSupportToDeadLetterQueue</c>, or <c>HasSupportToRequeue</c> members — the three
///      retired capability-gate properties (FR-10(4), AC-10(c)).
///   2. The four retained flag properties remain present — confirming the type was not emptied
///      or renamed, and that the cleanup was surgical.
/// </summary>
public class WhenGatesRetiredShouldAbsentConfigProperties
{
    [Fact]
    public void When_gates_retired_should_absent_config_properties()
    {
        // Arrange
        var type = typeof(MessagingGatewayConfiguration);

        // Act + Assert — the three retired properties are absent from the compiled surface (AC-10(c))
        Assert.True(
            type.GetProperty("HasSupportToDelayedMessages", BindingFlags.Public | BindingFlags.Instance) == null,
            "HasSupportToDelayedMessages must be absent — its gate was retired (ADR 0066 Step C, FR-10(4))");

        Assert.True(
            type.GetProperty("HasSupportToDeadLetterQueue", BindingFlags.Public | BindingFlags.Instance) == null,
            "HasSupportToDeadLetterQueue must be absent — its gate was retired (ADR 0066 Step C, FR-10(4))");

        Assert.True(
            type.GetProperty("HasSupportToRequeue", BindingFlags.Public | BindingFlags.Instance) == null,
            "HasSupportToRequeue must be absent — its gate was retired (ADR 0066 Step C, FR-10(4))");

        // Assert — the retained flag properties are still present (not emptied or renamed)
        Assert.True(
            type.GetProperty("HasSupportToPublishConfirmation", BindingFlags.Public | BindingFlags.Instance) != null,
            "HasSupportToPublishConfirmation must remain — it gates a retained template (confirming_posting)");

        Assert.True(
            type.GetProperty("HasSupportToValidateBrokerExistence", BindingFlags.Public | BindingFlags.Instance) != null,
            "HasSupportToValidateBrokerExistence must remain — it gates a retained template (no_broker_created)");

        Assert.True(
            type.GetProperty("HasSupportToValidateInfrastructure", BindingFlags.Public | BindingFlags.Instance) != null,
            "HasSupportToValidateInfrastructure must remain — it gates retained templates (assume_channel, validate_channel)");
    }
}
