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

public class ClaimCheckRequeueEnvelopeTests
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
    public async Task When_a_claimed_message_is_deferred_should_requeue_the_original_claim(
        bool useAsync, bool retain, bool dataRefOnly)
    {
        //Arrange
        using var scenario = new ClaimCheckPumpScenario(useAsync, retain, dataRefOnly);
        scenario.HandlerFailure = () => new DeferMessageAction();

        //Act
        scenario.Run();

        //Assert
        await Assert.That((await Assert.That(scenario.HandledPayloads).HasSingleItem())).IsEqualTo(scenario.Payload);
        await Assert.That(scenario.Channel.RequeueCount).IsEqualTo(1);
        await Assert.That(scenario.RequeuedBody).IsEqualTo(scenario.ClaimBody);
        var requeued = await Assert.That(scenario.RequeuedMessage).IsTypeOf<Message>();
        await Assert.That(requeued.Header.DataRef).IsEqualTo(scenario.ClaimId);
        if (dataRefOnly)
            await Assert.That(requeued.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK)).IsFalse();
        else
            await Assert.That(requeued.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK]).IsEqualTo(scenario.ClaimId);
        await Assert.That(requeued.Header.HandledCount).IsEqualTo(4);
        await Assert.That(requeued.Header.Bag["transport-lock"]).IsEqualTo("lock-to-preserve");
        await Assert.That(scenario.Channel.AcknowledgeCount).IsEqualTo(0);
    }
}
