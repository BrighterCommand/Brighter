#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Common;

public class ConfirmationSpanTests
{
    private readonly TracerProvider _traceProvider;
    private readonly BrighterTracer _tracer;
    private readonly ICollection<Activity> _exportedActivities;

    public ConfirmationSpanTests()
    {
        _exportedActivities = new List<Activity>();
        _traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        _tracer = new BrighterTracer();
    }

    [Fact]
    public void When_creating_a_confirmation_span_with_a_publish_context_should_link_to_it()
    {
        // Arrange — capture the context of a publish span, then close it so it cannot be
        // the ambient parent of the confirmation span (the confirmation span must LINK, not nest)
        var publishSpan = new ActivitySource("Paramore.Brighter.Tests").StartActivity("publish");
        var publishContext = publishSpan!.Context;
        publishSpan.Stop();
        Activity.Current = null;

        // Act
        var confirmationSpan = _tracer.CreateConfirmationSpan(
            new Id("message-id"),
            new RoutingKey("MyTopic"),
            success: true,
            links: new[] { new ActivityLink(publishContext) });

        // Assert — exactly one link, to the supplied publish context; the confirmation span is
        // a distinct, standalone activity that did not reopen or mutate the publish span
        Assert.NotNull(confirmationSpan);
        var links = confirmationSpan!.Links.ToArray();
        Assert.Single(links);
        Assert.Equal(publishContext, links[0].Context);
        Assert.NotEqual(publishSpan, confirmationSpan);
        Assert.NotEqual(publishSpan.Id, confirmationSpan.Id);
    }

    [Fact]
    public void When_creating_a_confirmation_span_under_a_live_ambient_span_should_be_a_root()
    {
        // Arrange — an ambient publish span is still OPEN and current when the confirmation
        // fires. This models the InMemory async-confirmation path, where ExecutionContext flows
        // the publish span (S1) into the Task.Run callback, so Activity.Current is non-null at the
        // moment CreateConfirmationSpan runs. The confirmation span must LINK to the publish span,
        // never NEST under it (or any other ambient activity).
        var publishSpan = new ActivitySource("Paramore.Brighter.Tests").StartActivity("publish");
        var publishContext = publishSpan!.Context;
        Activity.Current = publishSpan;

        // Act
        var confirmationSpan = _tracer.CreateConfirmationSpan(
            new Id("message-id"),
            new RoutingKey("MyTopic"),
            success: true,
            links: new[] { new ActivityLink(publishContext) });

        // Assert — the confirmation span is a root (no parent), despite the live ambient publish
        // span, and carries the publish context as a link rather than as its parent
        Assert.NotNull(confirmationSpan);
        Assert.Null(confirmationSpan!.Parent);
        Assert.Null(confirmationSpan.ParentId);
        Assert.Single(confirmationSpan.Links);
        Assert.Equal(publishContext, confirmationSpan.Links.ToArray()[0].Context);
    }

    [Fact]
    public void When_creating_a_confirmation_span_without_a_context_should_have_no_link()
    {
        // Arrange
        Activity.Current = null;

        // Act — no link supplied (the publish context was absent at send time)
        var confirmationSpan = _tracer.CreateConfirmationSpan(
            new Id("message-id"),
            new RoutingKey("MyTopic"),
            success: true,
            links: null);

        // Assert
        Assert.NotNull(confirmationSpan);
        Assert.Empty(confirmationSpan!.Links);
    }

    [Fact]
    public void When_creating_a_confirmation_span_should_set_it_as_the_current_activity_and_be_producer_kind()
    {
        // Arrange
        Activity.Current = null;

        // Act
        var confirmationSpan = _tracer.CreateConfirmationSpan(
            new Id("message-id"),
            new RoutingKey("MyTopic"),
            success: true,
            links: null);

        // Assert — becomes ambient so later work (the success-branch MarkDispatched DB span) nests under it
        Assert.Same(confirmationSpan, Activity.Current);
        Assert.Equal(ActivityKind.Producer, confirmationSpan!.Kind);
    }

    [Fact]
    public void When_creating_a_failure_confirmation_span_should_carry_id_topic_and_an_error_marker()
    {
        // Arrange
        Activity.Current = null;
        var id = new Id("message-id");
        var topic = new RoutingKey("MyTopic");

        // Act
        var confirmationSpan = _tracer.CreateConfirmationSpan(id, topic, success: false, links: null);

        // Assert — the failure outcome is recorded as error.type and Error status
        Assert.NotNull(confirmationSpan);
        Assert.Contains(confirmationSpan!.Tags, t => t.Key == BrighterSemanticConventions.MessageId && t.Value == id.Value);
        Assert.Contains(confirmationSpan.Tags, t => t.Key == BrighterSemanticConventions.MessagingDestination && t.Value == topic.Value);
        Assert.Contains(confirmationSpan.Tags, t => t.Key == BrighterSemanticConventions.ErrorType);
        Assert.Equal(ActivityStatusCode.Error, confirmationSpan.Status);
    }

    [Fact]
    public void When_creating_a_success_confirmation_span_should_not_carry_an_error_marker()
    {
        // Arrange
        Activity.Current = null;

        // Act
        var confirmationSpan = _tracer.CreateConfirmationSpan(
            new Id("message-id"),
            new RoutingKey("MyTopic"),
            success: true,
            links: null);

        // Assert — success carries no error.type and is not marked Error
        Assert.NotNull(confirmationSpan);
        Assert.DoesNotContain(confirmationSpan!.Tags, t => t.Key == BrighterSemanticConventions.ErrorType);
        Assert.NotEqual(ActivityStatusCode.Error, confirmationSpan.Status);
    }

    [Fact]
    public void When_creating_a_confirmation_span_with_an_empty_id_should_record_an_unknown_marker()
    {
        // Arrange
        Activity.Current = null;

        // Act — broker could not tell us which message failed (FR-5 degradation)
        var confirmationSpan = _tracer.CreateConfirmationSpan(
            Id.Empty,
            new RoutingKey("MyTopic"),
            success: false,
            links: null);

        // Assert — id recorded as an explicit "unknown" marker rather than an empty string
        Assert.NotNull(confirmationSpan);
        Assert.Contains(confirmationSpan!.Tags, t => t.Key == BrighterSemanticConventions.MessageId && t.Value == "unknown");
    }
}
