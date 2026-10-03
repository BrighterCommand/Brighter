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

public class ClaimCheckUnsettledDeliveryTests
{
    [Theory]
    [InlineData(false, "nack")]
    [InlineData(true, "nack")]
    [InlineData(false, "reject")]
    [InlineData(true, "reject")]
    [InlineData(false, "mapping-failure")]
    [InlineData(true, "mapping-failure")]
    [InlineData(false, "acknowledgement-failure")]
    [InlineData(true, "acknowledgement-failure")]
    [InlineData(false, "requeue-failure")]
    [InlineData(true, "requeue-failure")]
    public void When_a_claimed_delivery_is_not_completed_should_preserve_luggage(bool useAsync, string outcome)
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
        var error = Record.Exception(scenario.Run);

        //Assert
        if (scenario.Channel.FailAcknowledge)
            Assert.NotNull(error);
        else
            Assert.Null(error);
        Assert.Equal(0, scenario.Channel.AcknowledgeCount);
        if (outcome is "nack" or "requeue-failure")
            Assert.Single(scenario.Channel.Nacked);
        if (outcome is "reject" or "mapping-failure")
            Assert.Single(scenario.Channel.Rejected);
        if (outcome == "requeue-failure")
            Assert.Equal(1, scenario.Channel.RequeueCount);
        if (outcome == "mapping-failure")
            Assert.Empty(scenario.HandledPayloads);
        else
            Assert.Equal(scenario.Payload, Assert.Single(scenario.HandledPayloads));

        Assert.True(scenario.Storage.HasClaim(scenario.ClaimId));
        Assert.Equal(scenario.ClaimBody, scenario.Message.Body.Value);
        Assert.Equal(scenario.ClaimId, scenario.Message.Header.DataRef);
        Assert.Equal(scenario.ClaimId, scenario.Message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK]);
        Assert.NotEmpty(scenario.ScopedStores);
        Assert.All(scenario.ScopedStores, storage => Assert.True(storage.IsDisposed));
    }
}
