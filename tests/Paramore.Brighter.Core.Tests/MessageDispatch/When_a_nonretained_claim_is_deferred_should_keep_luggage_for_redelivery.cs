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

public class ClaimCheckDeferredLuggageTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void When_a_nonretained_claim_is_deferred_should_keep_luggage_for_redelivery(
        bool useAsync, bool aggregateException)
    {
        //Arrange
        using var scenario = new ClaimCheckPumpScenario(useAsync);
        scenario.HandlerFailure = () => aggregateException
            ? new AggregateException(new DeferMessageAction())
            : new DeferMessageAction();

        //Act
        scenario.Run();

        //Assert
        Assert.Equal(scenario.Payload, Assert.Single(scenario.HandledPayloads));
        Assert.Equal(1, scenario.Channel.RequeueCount);
        Assert.True(scenario.LuggageAtRequeue);
        Assert.True(scenario.Storage.HasClaim(scenario.ClaimId));
        Assert.NotEmpty(scenario.ScopedStores);
        Assert.All(scenario.ScopedStores, storage => Assert.True(storage.IsDisposed));
    }
}
