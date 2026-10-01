#region Licence
/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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
using Paramore.Brighter.Actions;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class ClaimCheckMapperMetadataTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_a_claim_is_unwrapped_by_a_pump_should_preserve_mapper_metadata(bool useAsync)
    {
        //Arrange
        using var scenario = new ClaimCheckPumpScenario(useAsync, dataRefOnly: true);

        //Act
        scenario.Run();

        //Assert
        Assert.Equal(scenario.Payload, Assert.Single(scenario.HandledPayloads));
        var mapped = Assert.IsType<Message>(scenario.MappedMessage);
        Assert.Equal(scenario.Message.Id, mapped.Id);
        Assert.Equal(scenario.Channel.RoutingKey, mapped.Header.Topic);
        Assert.Equal(new Uri("https://example.test/claim-source"), mapped.Header.Source);
        Assert.Equal(new CloudEventsType("claim.delivery"), mapped.Header.Type);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero), mapped.Header.TimeStamp);
        Assert.Equal(new Id("claim-correlation"), mapped.Header.CorrelationId);
        Assert.Equal("text/plain", mapped.Header.ContentType.MediaType);
        Assert.Equal("utf-8", mapped.Header.ContentType.CharSet);
        Assert.Equal(new RoutingKey("claim-reply"), mapped.Header.ReplyTo);
        Assert.Equal(3, mapped.Header.HandledCount);
        Assert.Equal(TimeSpan.FromSeconds(7), mapped.Header.Delayed);
        Assert.Equal(new PartitionKey("claim-partition"), mapped.Header.PartitionKey);
        Assert.Equal(new Uri("https://example.test/claim-schema"), mapped.Header.DataSchema);
        Assert.Equal("claim-subject", mapped.Header.Subject);
        Assert.Equal(new Id("claim-workflow"), mapped.Header.WorkflowId);
        Assert.Equal(new Id("claim-job"), mapped.Header.JobId);
        Assert.Equal("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", mapped.Header.TraceParent?.Value);
        Assert.Equal("vendor=value", mapped.Header.TraceState?.Value);
        Assert.Equal("tenant=sample", mapped.Header.Baggage.ToString());
        Assert.Equal("lock-to-preserve", mapped.Header.Bag["transport-lock"]);
        Assert.True(mapped.Persist);
    }
}
