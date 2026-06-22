#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ReplayCausationTrackingValidationTests
{
    [Fact]
    public void When_replay_configured_with_inbox_that_does_not_track_causation_should_report_error()
    {
        // Arrange — Replay action, but the inbox does not implement IAmACausationTrackingInbox
        var description = ReplayPipeline(OnceOnlyAction.Replay);
        var inbox = new NonTrackingInbox();
        var outbox = new TrackingOutbox(supportsCausationTracking: true);

        // Act
        var results = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox),
            description);

        // Assert
        var finding = Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
    }

    [Fact]
    public void When_replay_configured_with_inbox_that_does_not_support_causation_tracking_should_report_warning()
    {
        // Arrange — inbox implements the role but the live schema does not support it
        var description = ReplayPipeline(OnceOnlyAction.Replay);
        var inbox = new TrackingInbox(supportsCausationTracking: false);
        var outbox = new TrackingOutbox(supportsCausationTracking: true);

        // Act
        var results = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox),
            description);

        // Assert
        var finding = Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void When_replay_configured_with_no_outbox_should_report_warning()
    {
        // Arrange — Replay with a tracking inbox but no outbox configured (terminal step)
        var description = ReplayPipeline(OnceOnlyAction.Replay);
        var inbox = new TrackingInbox(supportsCausationTracking: true);

        // Act
        var results = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox: null),
            description);

        // Assert
        var finding = Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void When_replay_configured_with_outbox_that_does_not_track_causation_should_report_error()
    {
        // Arrange — outbox does not implement IAmACausationTrackingOutbox
        var description = ReplayPipeline(OnceOnlyAction.Replay);
        var inbox = new TrackingInbox(supportsCausationTracking: true);
        var outbox = new NonTrackingOutbox();

        // Act
        var results = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox),
            description);

        // Assert
        var finding = Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, finding.Severity);
    }

    [Fact]
    public void When_replay_configured_with_outbox_that_does_not_support_causation_tracking_should_report_warning()
    {
        // Arrange — outbox implements the role but the live schema does not support it
        var description = ReplayPipeline(OnceOnlyAction.Replay);
        var inbox = new TrackingInbox(supportsCausationTracking: true);
        var outbox = new TrackingOutbox(supportsCausationTracking: false);

        // Act
        var results = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox),
            description);

        // Assert
        var finding = Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void When_replay_configured_with_tracking_inbox_and_outbox_should_report_no_findings()
    {
        // Arrange — both inbox and outbox track and support causation
        var description = ReplayPipeline(OnceOnlyAction.Replay);
        var inbox = new TrackingInbox(supportsCausationTracking: true);
        var outbox = new TrackingOutbox(supportsCausationTracking: true);

        // Act
        var results = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox),
            description);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void When_non_replay_action_configured_without_causation_tracking_should_report_no_findings()
    {
        // Arrange — Throw action; causation tracking is irrelevant even with a non-tracking inbox/outbox
        var description = ReplayPipeline(OnceOnlyAction.Throw);
        var inbox = new NonTrackingInbox();
        var outbox = new NonTrackingOutbox();

        // Act
        var results = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox),
            description);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void When_replay_configured_and_inbox_support_probe_throws_should_report_warning_not_propagate()
    {
        // Arrange — the inbox tracks causation but its live-schema probe throws (e.g. the store is
        // unreachable at startup). Validation must degrade to a Warning, not let the exception escape.
        var description = ReplayPipeline(OnceOnlyAction.Replay);
        var inbox = new TrackingInbox(throwOnProbe: true);
        var outbox = new TrackingOutbox(supportsCausationTracking: true);

        // Act
        var results = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox),
            description);

        // Assert
        var finding = Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void When_replay_configured_and_outbox_support_probe_throws_should_report_warning_not_propagate()
    {
        // Arrange — the outbox tracks causation but its live-schema probe throws (unreachable store).
        var description = ReplayPipeline(OnceOnlyAction.Replay);
        var inbox = new TrackingInbox(supportsCausationTracking: true);
        var outbox = new TrackingOutbox(throwOnProbe: true);

        // Act
        var results = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox),
            description);

        // Assert
        var finding = Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, finding.Severity);
    }

    private static HandlerPipelineDescription ReplayPipeline(OnceOnlyAction onceOnlyAction)
        => new(
            requestType: typeof(MyCommand),
            handlerType: typeof(MyCommandHandler),
            isAsync: false,
            beforeSteps:
            [
                new PipelineStepDescription(
                    typeof(UseInboxAttribute),
                    typeof(UseInboxHandler<>),
                    Step: 0,
                    HandlerTiming.Before)
                {
                    Attribute = new UseInboxAttribute(
                        step: 0,
                        onceOnly: true,
                        onceOnlyAction: onceOnlyAction)
                }
            ],
            afterSteps: []);

    private static IReadOnlyList<ValidationError> Evaluate(
        ISpecification<HandlerPipelineDescription> spec,
        HandlerPipelineDescription description)
    {
        spec.IsSatisfiedBy(description);
        var collector = new ValidationResultCollector<HandlerPipelineDescription>();
        return spec.Accept(collector)
            .Where(r => !r.Success)
            .Select(r => r.Error!)
            .ToList();
    }
}

internal sealed class NonTrackingInbox : IAmAnInbox
{
    public IAmABrighterTracer Tracer { set { } }
}

internal sealed class TrackingInbox : IAmAnInbox, IAmACausationTrackingInbox
{
    private readonly bool _supportsCausationTracking;
    private readonly bool _throwOnProbe;

    public TrackingInbox(bool supportsCausationTracking = false, bool throwOnProbe = false)
    {
        _supportsCausationTracking = supportsCausationTracking;
        _throwOnProbe = throwOnProbe;
    }

    public IAmABrighterTracer Tracer { set { } }

    public bool SupportsCausationTracking()
        => _throwOnProbe
            ? throw new System.InvalidOperationException("inbox store unreachable")
            : _supportsCausationTracking;

    public Task<bool> SupportsCausationTrackingAsync(CancellationToken cancellationToken = default)
        => _throwOnProbe
            ? throw new System.InvalidOperationException("inbox store unreachable")
            : Task.FromResult(_supportsCausationTracking);

    public string? GetCausationId(string id, string contextKey,
        RequestContext? requestContext, int timeoutInMilliseconds = -1)
        => null;

    public Task<string?> GetCausationIdAsync(string id, string contextKey,
        RequestContext? requestContext, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}

internal sealed class NonTrackingOutbox : IAmAnOutbox
{
    public IAmABrighterTracer? Tracer { set { } }
}

internal sealed class TrackingOutbox : IAmAnOutbox, IAmACausationTrackingOutbox
{
    private readonly bool _supportsCausationTracking;
    private readonly bool _throwOnProbe;

    public TrackingOutbox(bool supportsCausationTracking = false, bool throwOnProbe = false)
    {
        _supportsCausationTracking = supportsCausationTracking;
        _throwOnProbe = throwOnProbe;
    }

    public IAmABrighterTracer? Tracer { set { } }

    public bool SupportsCausationTracking()
        => _throwOnProbe
            ? throw new System.InvalidOperationException("outbox store unreachable")
            : _supportsCausationTracking;

    public Task<bool> SupportsCausationTrackingAsync(CancellationToken cancellationToken = default)
        => _throwOnProbe
            ? throw new System.InvalidOperationException("outbox store unreachable")
            : Task.FromResult(_supportsCausationTracking);

    public void ReplayCausation(string causationId, RequestContext? requestContext,
        Dictionary<string, object>? args = null)
    { }

    public Task ReplayCausationAsync(string causationId, RequestContext? requestContext,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
