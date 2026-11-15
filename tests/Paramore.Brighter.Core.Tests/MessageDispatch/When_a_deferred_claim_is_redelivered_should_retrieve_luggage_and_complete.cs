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

public class ClaimCheckRedeliveryTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task When_a_deferred_claim_is_redelivered_should_retrieve_luggage_and_complete(bool useAsync, bool retain)
    {
        //Arrange
        using var scenario = new ClaimCheckPumpScenario(useAsync, retain, dataRefOnly: true);
        scenario.Channel.Redeliver = true;
        scenario.HandlerFailure = () => scenario.HandledPayloads.Count == 1 ? new DeferMessageAction() : null;

        //Act
        scenario.Run();

        //Assert
        await Assert.That(scenario.HandledPayloads).IsEqualTo(new[] { scenario.Payload, scenario.Payload });
        await Assert.That(scenario.Channel.RequeueCount).IsEqualTo(1);
        await Assert.That(scenario.RequeuedBody).IsEqualTo(scenario.ClaimBody);
        await Assert.That(scenario.LuggageAtRequeue).IsTrue();
        await Assert.That(scenario.Channel.AcknowledgeCount).IsEqualTo(1);
        await Assert.That((await Assert.That(scenario.LuggageAtAcknowledge).HasSingleItem())).IsTrue();
        await Assert.That(scenario.Storage.HasClaim(scenario.ClaimId)).IsEqualTo(retain);
        await Assert.That(scenario.Channel.Rejected).IsEmpty();
        foreach (var storage in scenario.ScopedStores)
{
    await Assert.That(storage.IsDisposed).IsTrue();
}
    }
}
