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

public class ClaimCheckUnsettledDeliveryTests
{
    [Test]
    [Arguments(false, "nack")]
    [Arguments(true, "nack")]
    [Arguments(false, "reject")]
    [Arguments(true, "reject")]
    [Arguments(false, "mapping-failure")]
    [Arguments(true, "mapping-failure")]
    [Arguments(false, "acknowledgement-failure")]
    [Arguments(true, "acknowledgement-failure")]
    [Arguments(false, "requeue-failure")]
    [Arguments(true, "requeue-failure")]
    public async Task When_a_claimed_delivery_is_not_completed_should_preserve_luggage(bool useAsync, string outcome)
    {
        //Arrange
        using var scenario = new ClaimCheckPumpScenario(useAsync);
        scenario.HandlerFailure = () => outcome switch
        {
            "nack" => new DontAckAction(),
            "reject" => new RejectMessageAction(),
            "requeue-failure" => new DeferMessageAction(),
            _ => null
        };
        scenario.MappingFailure = outcome == "mapping-failure";
        scenario.Channel.FailAcknowledge = outcome == "acknowledgement-failure";
        scenario.Channel.FailRequeue = outcome == "requeue-failure";

        //Act
        var error = TestExceptionRecorder.Capture(scenario.Run);

        //Assert
        if (scenario.Channel.FailAcknowledge)
            await Assert.That(error).IsNotNull();
        else
            await Assert.That(error).IsNull();
        await Assert.That(scenario.Channel.AcknowledgeCount).IsEqualTo(0);
        if (outcome is "nack" or "requeue-failure")
            await Assert.That(scenario.Channel.Nacked).HasSingleItem();
        if (outcome is "reject" or "mapping-failure")
            await Assert.That(scenario.Channel.Rejected).HasSingleItem();
        if (outcome == "requeue-failure")
            await Assert.That(scenario.Channel.RequeueCount).IsEqualTo(1);
        if (outcome == "mapping-failure")
            await Assert.That(scenario.HandledPayloads).IsEmpty();
        else
            await Assert.That((await Assert.That(scenario.HandledPayloads).HasSingleItem())).IsEqualTo(scenario.Payload);

        await Assert.That(scenario.Storage.HasClaim(scenario.ClaimId)).IsTrue();
        await Assert.That(scenario.Message.Body.Value).IsEqualTo(scenario.ClaimBody);
        await Assert.That(scenario.Message.Header.DataRef).IsEqualTo(scenario.ClaimId);
        await Assert.That(scenario.Message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK]).IsEqualTo(scenario.ClaimId);
        await Assert.That(scenario.ScopedStores).IsNotEmpty();
        foreach (var storage in scenario.ScopedStores)
{
    await Assert.That(storage.IsDisposed).IsTrue();
}
    }
}
