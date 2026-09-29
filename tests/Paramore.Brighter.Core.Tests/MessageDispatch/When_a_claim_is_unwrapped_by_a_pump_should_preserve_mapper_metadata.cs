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
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class ClaimCheckMapperMetadataTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_a_claim_is_unwrapped_by_a_pump_should_preserve_mapper_metadata(bool useAsync)
    {
        //Arrange
        using var scenario = new ClaimCheckPumpScenario(useAsync, dataRefOnly: true);

        //Act
        scenario.Run();

        //Assert
        await Assert.That((await Assert.That(scenario.HandledPayloads).HasSingleItem())).IsEqualTo(scenario.Payload);
        var mapped = await Assert.That(scenario.MappedMessage).IsTypeOf<Message>();
        await Assert.That(mapped.Id).IsEqualTo(scenario.Message.Id);
        await Assert.That(mapped.Header.Topic).IsEqualTo(scenario.Channel.RoutingKey);
        await Assert.That(mapped.Header.Source).IsEqualTo(new Uri("https://example.test/claim-source"));
        await Assert.That(mapped.Header.Type).IsEqualTo(new CloudEventsType("claim.delivery"));
        await Assert.That(mapped.Header.TimeStamp).IsEqualTo(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
        await Assert.That(mapped.Header.CorrelationId).IsEqualTo(new Id("claim-correlation"));
        await Assert.That(mapped.Header.ContentType.MediaType).IsEqualTo("text/plain");
        await Assert.That(mapped.Header.ContentType.CharSet).IsEqualTo("utf-8");
        await Assert.That(mapped.Header.ReplyTo).IsEqualTo(new RoutingKey("claim-reply"));
        await Assert.That(mapped.Header.HandledCount).IsEqualTo(3);
        await Assert.That(mapped.Header.Delayed).IsEqualTo(TimeSpan.FromSeconds(7));
        await Assert.That(mapped.Header.PartitionKey).IsEqualTo(new PartitionKey("claim-partition"));
        await Assert.That(mapped.Header.DataSchema).IsEqualTo(new Uri("https://example.test/claim-schema"));
        await Assert.That(mapped.Header.Subject).IsEqualTo("claim-subject");
        await Assert.That(mapped.Header.WorkflowId).IsEqualTo(new Id("claim-workflow"));
        await Assert.That(mapped.Header.JobId).IsEqualTo(new Id("claim-job"));
        await Assert.That(mapped.Header.TraceParent?.Value).IsEqualTo("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
        await Assert.That(mapped.Header.TraceState?.Value).IsEqualTo("vendor=value");
        await Assert.That(mapped.Header.Baggage.ToString()).IsEqualTo("tenant=sample");
        await Assert.That(mapped.Header.Bag["transport-lock"]).IsEqualTo("lock-to-preserve");
        await Assert.That(mapped.Persist).IsTrue();
    }
}
