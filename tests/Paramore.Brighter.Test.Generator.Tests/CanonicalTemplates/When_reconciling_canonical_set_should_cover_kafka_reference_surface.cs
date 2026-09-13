using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Coverage reconciliation checkpoint (FR-21, AC-13, AC-21).
///
/// Verifies that every behavioural row of the Kafka reference surface
/// (requirements.md "Coverage Reconciliation (Kafka reference surface)" table)
/// maps to a canonical template in both the Reactor and Proactor directories,
/// and that the mechanism-only scheduler test (OOS-2) and transport-internal
/// tests (OOS-3) are NOT reproduced as canonical templates.
///
/// This is a static check on the template sources. It does not invoke the
/// generator; it confirms that the canonical set is structurally complete.
///
/// Coverage Reconciliation (Kafka reference surface → canonical requirement):
///   ..._requeues_with_delay_should_use_producer         → FR-2
///   ..._requeues_with_delay_should_use_scheduler        → — (OOS-2, mechanism assertion)
///   ..._delivery_error_should_send_to_dlq               → FR-4
///   ..._unacceptable_reason_should_send_to_invalid_channel → FR-5
///   ..._unacceptable_and_no_invalid_channel_...         → FR-6
///   ..._no_channels_configured_should_acknowledge_and_log → FR-7
///   ..._rejecting_message_should_include_metadata       → FR-8
///   ..._unknown_reason_should_send_to_dlq               → FR-17
///   When_nacking_a_message_it_should_be_redelivered     → FR-16
///   plain requeue / delayed send                        → FR-22 / FR-9
///   (zero-delay boundary — FR-15 adds the explicit TimeSpan.Zero arm)
/// </summary>
public class KafkaReferenceSurfaceReconciliationTests
{
    // The eleven canonical template names, one per Kafka reference behaviour
    // (Coverage Reconciliation table, requirements.md): FR-2, FR-4, FR-5, FR-6,
    // FR-7, FR-8, FR-9, FR-15, FR-16, FR-17, FR-22.
    // Each MUST exist in both the Reactor and Proactor directories (FR-14).
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

    // Mechanism terms that must never appear in any canonical template body (AC-21, NFR-3, OOS-1).
    // Any occurrence is a mechanism assertion that violates the suite's scope.
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
        // Locate template directories via AppContext.BaseDirectory — the same resolution
        // used by the generator when it runs tests (ADR 0066 implementation notes).
        var gatewayTemplatesRoot = Path.Combine(
            AppContext.BaseDirectory, "Templates", "MessagingGateway");
        _reactorDir = Path.Combine(gatewayTemplatesRoot, "Reactor");
        _proactorDir = Path.Combine(gatewayTemplatesRoot, "Proactor");
    }

    [Fact]
    public void When_reconciling_canonical_set_should_cover_kafka_reference_surface()
    {
        // Arrange — template directories are resolved in the constructor

        // Assert — every behavioural row in the Coverage Reconciliation table has a canonical
        // template in both the Reactor and Proactor directories (FR-14, FR-21)
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
        // Arrange — enumerate all canonical template files (both variants)
        var canonicalFiles =
            CANONICAL_TEMPLATE_NAMES
                .SelectMany(name => new[]
                {
                    Path.Combine(_reactorDir, $"{name}.cs.liquid"),
                    Path.Combine(_proactorDir, $"{name}.cs.liquid"),
                })
                .Where(File.Exists)
                .ToList();

        // Assert — no canonical template body contains any mechanism term (AC-21, NFR-3, OOS-1).
        // A scheduler, native-delay API, or redrive policy reference is a mechanism assertion
        // that violates the mechanism-agnostic conformance suite contract.
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
        // Arrange — canonical template directories are resolved in the constructor

        // Assert — no template in either canonical directory is named for the OOS-2
        // scheduler-delegation behaviour. The Kafka hand-written test
        // When_kafka_consumer_requeues_with_delay_should_use_scheduler is a mechanism assertion
        // (OOS-2) and must NOT be reproduced as a canonical template (requirements.md OOS-2,
        // ADR 0066 "Why there is no scheduler member").
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
