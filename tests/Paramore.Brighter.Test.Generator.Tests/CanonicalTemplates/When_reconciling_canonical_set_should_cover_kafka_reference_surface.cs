using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// A static check on the template sources: every behaviour of the Kafka reference surface has a canonical
/// template in both the Reactor and Proactor directories, and no canonical template names a mechanism or
/// reproduces the scheduler-delegation test. See ADR 0066, Why there is no scheduler member.
/// </summary>
public class KafkaReferenceSurfaceReconciliationTests
{
    // One per Kafka reference behaviour, labelled with the ledger column it is judged against.
    private static readonly string[] CANONICAL_TEMPLATE_NAMES =
    [
        "When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay",              // FR-2
        "When_rejecting_message_with_delivery_error_should_send_to_dlq",                        // FR-4
        "When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel",       // FR-5
        "When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq", // FR-6
        "When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log",        // FR-7
        "When_rejecting_message_should_include_metadata",                                       // FR-8
        "When_sending_a_delayed_message_should_deliver_after_delay",                            // FR-9
        "When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately",         // FR-15
        "When_nacking_a_message_it_should_be_redelivered",                                      // FR-16
        "When_rejecting_message_with_unknown_reason_should_send_to_dlq",                        // FR-17
        "When_requeuing_a_failed_message_should_be_redelivered",                                // FR-22
    ];

    // Naming any of these in a canonical template asserts a mechanism, not a behaviour.
    private static readonly string[] MECHANISM_TERMS =
    [
        "IAmAMessageScheduler",
        "IAmAMessageSchedulerSync",
        "IAmAMessageSchedulerAsync",
        "SpyScheduler",
        "RedrivePolicy",
        "ModifyAckDeadline",
        "ChangeInvisibleDuration",
        "ChangeMessageVisibility",
        "FakeTimeProvider",
        "IAmAChannelFactoryWithScheduler",
    ];

    private readonly string _reactorDir;
    private readonly string _proactorDir;

    public KafkaReferenceSurfaceReconciliationTests()
    {
        var gatewayTemplatesRoot = Path.Combine(
            AppContext.BaseDirectory, "Templates", "MessagingGateway");
        _reactorDir = Path.Combine(gatewayTemplatesRoot, "Reactor");
        _proactorDir = Path.Combine(gatewayTemplatesRoot, "Proactor");
    }

    [Fact]
    public void When_reconciling_canonical_set_should_cover_kafka_reference_surface()
    {
        // Assert
        foreach (var templateName in CANONICAL_TEMPLATE_NAMES)
        {
            var reactorTemplate = Path.Combine(_reactorDir, $"{templateName}.cs.liquid");
            var proactorTemplate = Path.Combine(_proactorDir, $"{templateName}.cs.liquid");

            Assert.True(File.Exists(reactorTemplate),
                $"Reactor canonical template absent for Kafka reference surface: {templateName}");

            Assert.True(File.Exists(proactorTemplate),
                $"Proactor canonical template absent for Kafka reference surface: {templateName}");
        }
    }

    [Fact]
    public void When_reconciling_canonical_set_no_canonical_template_body_should_reference_scheduler_or_mechanism()
    {
        // Arrange
        var canonicalFiles =
            CANONICAL_TEMPLATE_NAMES
                .SelectMany(name => new[]
                {
                    Path.Combine(_reactorDir, $"{name}.cs.liquid"),
                    Path.Combine(_proactorDir, $"{name}.cs.liquid"),
                })
                .Where(File.Exists)
                .ToList();

        // Assert
        foreach (var templatePath in canonicalFiles)
        {
            var content = File.ReadAllText(templatePath);

            foreach (var term in MECHANISM_TERMS)
            {
                Assert.DoesNotContain(term, content, System.StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void When_reconciling_canonical_set_no_scheduler_delegation_template_should_exist_as_canonical()
    {
        // Assert
        var reactorSchedulerTemplates =
            Directory.GetFiles(_reactorDir, "*.cs.liquid")
                .Where(f => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f))
                    .Contains("should_use_scheduler", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

        var proactorSchedulerTemplates =
            Directory.GetFiles(_proactorDir, "*.cs.liquid")
                .Where(f => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f))
                    .Contains("should_use_scheduler", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

        Assert.Empty(reactorSchedulerTemplates);
        Assert.Empty(proactorSchedulerTemplates);
    }
}
