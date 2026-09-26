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
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScheduledRequestContextTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
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
        Assert.Empty(Received(provider, isAsync));
        timeProvider.Advance(delay);

        //Assert
        var restored = Assert.Single(Received(provider, isAsync));
        Assert.NotSame(context, restored);
        Assert.Equal(3, restored.GetHeaders()!["x-attempt"]);
        Assert.Equal("tenant-1", restored.GetCloudEventAdditionalProperties()!["tenant"]);
        Assert.Equal("partition-1", restored.GetPartitionKey().Value);
        Assert.Equal(causationId, restored.Bag[RequestContextBagNames.CausationId]);
        Assert.Equal(jobId, restored.GetJobId());
        Assert.Equal(workflowId, restored.GetWorkflowId());
        Assert.False(restored.Bag.ContainsKey("custom-value"));

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
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
        var context = Assert.Single(Received(provider, isAsync));
        Assert.Null(context.GetHeaders());
        Assert.Equal(PartitionKey.Empty, context.GetPartitionKey());
    }

    public static TheoryData<string, bool, bool> MetadataValueCases
    {
        get
        {
            var cases = new TheoryData<string, bool, bool>();
            foreach (var name in new[] { "null", "string", "guid-string", "date-string", "char", "bool", "byte", "sbyte",
                         "short", "ushort", "int", "uint", "small-long", "large-long", "ulong", "float", "double",
                         "large-double", "decimal", "guid", "datetime", "offset", "bytes", "timespan", "uri" })
                foreach (var isAsync in new[] { false, true })
                    foreach (var cloudEvents in new[] { false, true })
                        cases.Add(name, isAsync, cloudEvents);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(MetadataValueCases))]
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
        var restored = Assert.Single(Received(provider, isAsync));
        var metadata = cloudEvents ? restored.GetCloudEventAdditionalProperties() : restored.GetHeaders();
        var actual = Assert.Single(metadata!).Value;
        Assert.True(metadata!.ContainsKey("ValueKey"));
        if (expected == null)
        {
            Assert.Null(actual);
            return;
        }
        Assert.IsType(expected.GetType(), actual);
        Assert.Equal(expected, actual);
        if (expected is DateTimeOffset offset)
            Assert.Equal(offset.Offset, ((DateTimeOffset)actual).Offset);
        if (expected is DateTime dateTime)
            Assert.Equal(dateTime.Kind, ((DateTime)actual).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
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
        var restored = Assert.Single(Received(provider, false));
        var metadata = cloudEvents ? restored.GetCloudEventAdditionalProperties() : restored.GetHeaders();
        Assert.True(metadata!.TryGetValue("TENANT", out var value));
        Assert.Equal("tenant-1", value);
        Assert.Equal("Tenant", Assert.Single(metadata).Key);
    }

    public static TheoryData<string, bool, bool> UnsupportedMetadataCases
    {
        get
        {
            var cases = new TheoryData<string, bool, bool>();
            foreach (var name in new[] { "delegate", "type", "enum", "object", "array", "cycle", "nan", "infinity" })
                foreach (var isAsync in new[] { false, true })
                    foreach (var cloudEvents in new[] { false, true })
                        cases.Add(name, isAsync, cloudEvents);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(UnsupportedMetadataCases))]
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
            ? await Record.ExceptionAsync(() => processor.SendAsync(TimeSpan.FromSeconds(1), new ScheduledContextEventAsync(), context))
            : Record.Exception(() => processor.Send(TimeSpan.FromSeconds(1), new ScheduledContextEvent(), context));
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        //Assert
        Assert.IsType<JsonException>(exception);
        Assert.Contains("unsupported", exception.Message);
        Assert.Empty(Received(provider, isAsync));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
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
        var exception = Record.Exception(() => processor.Send(TimeSpan.FromSeconds(1), new ScheduledContextEvent(), context));
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        //Assert
        Assert.IsType<JsonException>(exception);
        Assert.Contains("comparer", exception.Message);
        Assert.Empty(Received(provider, false));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"headers":{"caseInsensitive":false,"values":{"tenant":{"type":"unknown","value":"tenant-1"}}}}""")]
    [InlineData("""{"headers":{"caseInsensitive":false,"values":{"tenant":{"type":"int64","value":"not-a-number"}}}}""")]
    [InlineData("""{"headers":{"caseInsensitive":true,"values":{"Tenant":null,"TENANT":null}}}""")]
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
        var exception = await Record.ExceptionAsync(() => processor.SendAsync(command));

        //Assert
        Assert.IsType<JsonException>(exception);
        Assert.Empty(Received(provider, false));
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
