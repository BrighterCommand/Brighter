#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Scheduler.Events;


namespace Paramore.Brighter.Extensions.Tests;

[System.Obsolete]
public class ScheduledRequestContextTests
{
    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, false)]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
    public async Task When_scheduling_a_request_should_preserve_supported_context_metadata(bool publish, bool isAsync, bool useDateTime)
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        await using var provider = BuildProvider(timeProvider);
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var headers = new Dictionary<string, object> { ["x-attempt"] = 3 };
        var properties = new Dictionary<string, object> { ["tenant"] = "tenant-1" };
        var context = new RequestContext();
        context.Bag[RequestContextBagNames.Headers] = headers;
        context.Bag[RequestContextBagNames.CloudEventsAdditionalProperties] = properties;
        context.Bag[RequestContextBagNames.PartitionKey] = new PartitionKey("partition-1");
        var jobId = Id.Random();
        var workflowId = Id.Random();
        var causationId = Id.Random();
        context.Bag[RequestContextBagNames.CausationId] = causationId;
        context.Bag[RequestContextBagNames.JobId] = jobId;
        context.Bag[RequestContextBagNames.WorkflowId] = workflowId;
        context.Bag["custom-value"] = 42;
        var delay = TimeSpan.FromSeconds(1);
        var at = timeProvider.GetUtcNow().Add(delay);

        //Act
        if (isAsync)
            await ScheduleAsync(new ScheduledContextEventAsync());
        else
            await ScheduleAsync(new ScheduledContextEvent());

        headers["x-attempt"] = 4;
        properties["tenant"] = "changed";
        context.Bag["custom-value"] = 99;
        await Assert.That(Received(provider, isAsync)).IsEmpty();
        timeProvider.Advance(delay);

        //Assert
        var restored = await Assert.That(Received(provider, isAsync)).HasSingleItem();
        await Assert.That(restored).IsNotSameReferenceAs(context);
        await Assert.That(restored.GetHeaders()!["x-attempt"]).IsEqualTo(3);
        await Assert.That(restored.GetCloudEventAdditionalProperties()!["tenant"]).IsEqualTo("tenant-1");
        await Assert.That(restored.GetPartitionKey().Value).IsEqualTo("partition-1");
        await Assert.That(restored.Bag[RequestContextBagNames.CausationId]).IsEqualTo(causationId);
        await Assert.That(restored.GetJobId()).IsEqualTo(jobId);
        await Assert.That(restored.GetWorkflowId()).IsEqualTo(workflowId);
        await Assert.That(restored.Bag.ContainsKey("custom-value")).IsFalse();

        async Task ScheduleAsync<TRequest>(TRequest request) where TRequest : class, IRequest
        {
            _ = (publish, isAsync, useDateTime) switch
            {
                (false, false, false) => processor.Send(delay, request, context),
                (false, false, true) => processor.Send(at, request, context),
                (false, true, false) => await processor.SendAsync(delay, request, context),
                (false, true, true) => await processor.SendAsync(at, request, context),
                (true, false, false) => processor.Publish(delay, request, context),
                (true, false, true) => processor.Publish(at, request, context),
                (true, true, false) => await processor.PublishAsync(delay, request, context),
                (true, true, true) => await processor.PublishAsync(at, request, context)
            };
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_firing_a_legacy_scheduled_request_should_handle_it_without_context_data(bool isAsync)
    {
        //Arrange
        await using var provider = BuildProvider(new FakeTimeProvider());
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var command = new FireSchedulerRequest
        {
            Async = isAsync,
            SchedulerType = RequestSchedulerType.Send,
            RequestType = (isAsync ? typeof(ScheduledContextEventAsync) : typeof(ScheduledContextEvent)).FullName!,
            RequestData = JsonSerializer.Serialize(new ScheduledContextEvent(), JsonSerialisationOptions.Options)
        };

        //Act
        await processor.SendAsync(command);

        //Assert
        var context = await Assert.That(Received(provider, isAsync)).HasSingleItem();
        await Assert.That(context.GetHeaders()).IsNull();
        await Assert.That(context.GetPartitionKey()).IsEqualTo(PartitionKey.Empty);
    }

    public static IEnumerable<(string, bool, bool)> MetadataValueCases
    {
        get
        {
            var cases = new List<(string, bool, bool)>();
            foreach (var name in new[] { "null", "string", "guid-string", "date-string", "char", "bool", "byte", "sbyte",
                         "short", "ushort", "int", "uint", "small-long", "large-long", "ulong", "float", "double",
                         "large-double", "decimal", "guid", "datetime", "offset", "bytes", "timespan", "uri" })
                foreach (var isAsync in new[] { false, true })
                    foreach (var cloudEvents in new[] { false, true })
                        cases.Add((name, isAsync, cloudEvents));
            return cases;
        }
    }

    [Test]
    [MethodDataSource(nameof(MetadataValueCases))]
    public async Task When_scheduling_a_request_should_preserve_metadata_value_types(string name, bool isAsync, bool cloudEvents)
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        await using var provider = BuildProvider(timeProvider);
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var value = MetadataValue(name);
        var expected = value is byte[] bytes ? bytes.Clone() : value;
        var key = cloudEvents ? RequestContextBagNames.CloudEventsAdditionalProperties : RequestContextBagNames.Headers;
        var context = new RequestContext();
        context.Bag[key] = new Dictionary<string, object> { ["ValueKey"] = value! };
        var delay = TimeSpan.FromSeconds(1);

        //Act
        if (isAsync)
            await processor.SendAsync(delay, new ScheduledContextEventAsync(), context);
        else
            processor.Send(delay, new ScheduledContextEvent(), context);
        if (value is byte[] mutableBytes)
            mutableBytes[0] = 99;
        timeProvider.Advance(delay);

        //Assert
        var restored = await Assert.That(Received(provider, isAsync)).HasSingleItem();
        var metadata = cloudEvents ? restored.GetCloudEventAdditionalProperties() : restored.GetHeaders();
        var actual = (await Assert.That(metadata!).HasSingleItem()).Value;
        await Assert.That(metadata!.ContainsKey("ValueKey")).IsTrue();
        if (expected == null)
        {
            await Assert.That(actual).IsNull();
            return;
        }
        await Assert.That(actual.GetType()).IsEqualTo(expected.GetType());
        if (expected is byte[] expectedBytes)
            await Assert.That((byte[])actual).IsEquivalentTo(expectedBytes, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        else
            await Assert.That(actual).IsEqualTo(expected);
        if (expected is DateTimeOffset offset)
            await Assert.That(((DateTimeOffset)actual).Offset).IsEqualTo(offset.Offset);
        if (expected is DateTime dateTime)
            await Assert.That(((DateTime)actual).Kind).IsEqualTo(dateTime.Kind);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_scheduling_case_insensitive_metadata_should_preserve_key_lookup(bool cloudEvents)
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        await using var provider = BuildProvider(timeProvider);
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var context = new RequestContext();
        var key = cloudEvents ? RequestContextBagNames.CloudEventsAdditionalProperties : RequestContextBagNames.Headers;
        context.Bag[key] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["Tenant"] = "tenant-1" };

        //Act
        processor.Send(TimeSpan.FromSeconds(1), new ScheduledContextEvent(), context);
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        //Assert
        var restored = await Assert.That(Received(provider, false)).HasSingleItem();
        var metadata = cloudEvents ? restored.GetCloudEventAdditionalProperties() : restored.GetHeaders();
        await Assert.That(metadata!.TryGetValue("TENANT", out var value)).IsTrue();
        await Assert.That(value).IsEqualTo("tenant-1");
        await Assert.That((await Assert.That(metadata).HasSingleItem()).Key).IsEqualTo("Tenant");
    }

    public static IEnumerable<(string, bool, bool)> UnsupportedMetadataCases
    {
        get
        {
            var cases = new List<(string, bool, bool)>();
            foreach (var name in new[] { "delegate", "type", "enum", "object", "array", "cycle", "nan", "infinity" })
                foreach (var isAsync in new[] { false, true })
                    foreach (var cloudEvents in new[] { false, true })
                        cases.Add((name, isAsync, cloudEvents));
            return cases;
        }
    }

    [Test]
    [MethodDataSource(nameof(UnsupportedMetadataCases))]
    public async Task When_scheduling_unsupported_metadata_should_fail_before_scheduling(string name, bool isAsync, bool cloudEvents)
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        await using var provider = BuildProvider(timeProvider);
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var cyclic = new Dictionary<string, object>();
        cyclic["self"] = cyclic;
        object value = name switch
        {
            "delegate" => (Action)(() => { }),
            "type" => typeof(string),
            "enum" => DayOfWeek.Monday,
            "object" => new { Text = "value" },
            "array" => new[] { 1, 2 },
            "cycle" => cyclic,
            "nan" => double.NaN,
            "infinity" => float.PositiveInfinity,
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        var key = cloudEvents ? RequestContextBagNames.CloudEventsAdditionalProperties : RequestContextBagNames.Headers;
        var context = new RequestContext();
        context.Bag[key] = new Dictionary<string, object> { ["unsupported"] = value };

        //Act
        var exception = isAsync
            ? await TestExceptionRecorder.CaptureAsync(() => processor.SendAsync(TimeSpan.FromSeconds(1), new ScheduledContextEventAsync(), context))
            : TestExceptionRecorder.Capture(() => processor.Send(TimeSpan.FromSeconds(1), new ScheduledContextEvent(), context));
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        //Assert
        await Assert.That(exception).IsTypeOf<JsonException>();
        await Assert.That(exception.Message).Contains("unsupported");
        await Assert.That(Received(provider, isAsync)).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_scheduling_metadata_with_a_culture_comparer_should_fail_before_scheduling(bool cloudEvents)
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        await using var provider = BuildProvider(timeProvider);
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var context = new RequestContext();
        var key = cloudEvents ? RequestContextBagNames.CloudEventsAdditionalProperties : RequestContextBagNames.Headers;
        context.Bag[key] = new Dictionary<string, object>(StringComparer.InvariantCulture) { ["tenant"] = "tenant-1" };

        //Act
        Exception? exception = null;
        try
        {
            processor.Send(TimeSpan.FromSeconds(1), new ScheduledContextEvent(), context);
        }
        catch (Exception e)
        {
            exception = e;
        }
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        //Assert
        await Assert.That(exception).IsTypeOf<JsonException>();
        await Assert.That(exception.Message).Contains("comparer");
        await Assert.That(Received(provider, false)).IsEmpty();
    }

    [Test]
    [Arguments("{")]
    [Arguments("""{"headers":{"caseInsensitive":false,"values":{"tenant":{"type":"unknown","value":"tenant-1"}}}}""")]
    [Arguments("""{"headers":{"caseInsensitive":false,"values":{"tenant":{"type":"int64","value":"not-a-number"}}}}""")]
    [Arguments("""{"headers":{"caseInsensitive":true,"values":{"Tenant":null,"TENANT":null}}}""")]
    public async Task When_firing_invalid_context_data_should_fail_without_dispatching_the_request(string contextData)
    {
        //Arrange
        await using var provider = BuildProvider(new FakeTimeProvider());
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var command = new FireSchedulerRequest
        {
            SchedulerType = RequestSchedulerType.Send,
            RequestType = typeof(ScheduledContextEvent).FullName!,
            RequestData = JsonSerializer.Serialize(new ScheduledContextEvent(), JsonSerialisationOptions.Options),
            RequestContextData = contextData
        };

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(() => processor.SendAsync(command));

        //Assert
        await Assert.That(exception).IsTypeOf<JsonException>();
        await Assert.That(Received(provider, false)).IsEmpty();
    }

    private static object? MetadataValue(string name) => name switch
    {
        "null" => null,
        "string" => "tenant-1",
        "guid-string" => "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
        "date-string" => "2026-09-26",
        "char" => 'A',
        "bool" => true,
        "byte" => byte.MaxValue,
        "sbyte" => sbyte.MinValue,
        "short" => short.MinValue,
        "ushort" => ushort.MaxValue,
        "int" => int.MinValue,
        "uint" => uint.MaxValue,
        "small-long" => 42L,
        "large-long" => long.MaxValue,
        "ulong" => ulong.MaxValue,
        "float" => 1.5f,
        "double" => 1.5d,
        "large-double" => 1e30d,
        "decimal" => decimal.MaxValue,
        "guid" => Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"),
        "datetime" => new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc),
        "offset" => new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.FromHours(5.5)),
        "bytes" => new byte[] { 1, 2, 3 },
        "timespan" => TimeSpan.FromSeconds(1.5),
        "uri" => new Uri("https://example.org/resource"),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static ServiceProvider BuildProvider(FakeTimeProvider timeProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ScheduledContextEventHandler>();
        services.AddSingleton<ScheduledContextEventHandlerAsync>();
        services.AddBrighter()
            .UseScheduler(new InMemorySchedulerFactory { TimeProvider = timeProvider });
        return services.BuildServiceProvider();
    }

    private static List<IRequestContext> Received(ServiceProvider provider, bool isAsync)
        => isAsync ? provider.GetRequiredService<ScheduledContextEventHandlerAsync>().Received
            : provider.GetRequiredService<ScheduledContextEventHandler>().Received;
}
