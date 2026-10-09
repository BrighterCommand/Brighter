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

public class ClaimCheckRequeueEnvelopeTests
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
    public void When_a_claimed_message_is_deferred_should_requeue_the_original_claim(
        bool useAsync, bool retain, bool dataRefOnly)
    {
        //Arrange
        using var scenario = new ClaimCheckPumpScenario(useAsync, retain, dataRefOnly);
        scenario.HandlerFailure = () => new DeferMessageAction();

        //Act
        scenario.Run();

        //Assert
        Assert.Equal(scenario.Payload, Assert.Single(scenario.HandledPayloads));
        Assert.Equal(1, scenario.Channel.RequeueCount);
        Assert.Equal(scenario.ClaimBody, scenario.RequeuedBody);
        var requeued = Assert.IsType<Message>(scenario.RequeuedMessage);
        Assert.Equal(scenario.ClaimId, requeued.Header.DataRef);
        if (dataRefOnly)
            Assert.False(requeued.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK));
        else
            Assert.Equal(scenario.ClaimId, requeued.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK]);
        Assert.Equal(4, requeued.Header.HandledCount);
        Assert.Equal("lock-to-preserve", requeued.Header.Bag["transport-lock"]);
        Assert.Equal(0, scenario.Channel.AcknowledgeCount);
    }
}
