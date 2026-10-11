#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

using System;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox;

public class CausationScenarioIsolationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_causation_scenarios_share_a_store_should_not_replay_each_others_messages(
        bool isAsync, bool bulk)
    {
        // Arrange
        var outbox = new InMemoryInterleavedReplayOutbox(TimeProvider.System);
        using var first = new SharedStoreScenario(outbox);
        using var second = new SharedStoreScenario(outbox);
        var interleaved = false;
        outbox.BeforeNextOutstandingRead = async () =>
        {
            await ReplayScenario(second, isAsync, bulk);
            interleaved = true;
        };

        // Act
        var exception = await Record.ExceptionAsync(() => ReplayScenario(first, isAsync, bulk));

        // Assert
        Assert.True(interleaved);
        Assert.Null(exception);
    }

    private static Task ReplayScenario(SharedStoreScenario scenario, bool isAsync, bool bulk)
    {
        if (isAsync)
            return bulk
                ? scenario.When_replaying_causation_for_messages_deposited_in_bulk_should_clear_dispatch_state_async()
                : scenario.When_replaying_causation_on_outbox_should_clear_dispatch_state_async();

        if (bulk)
            scenario.When_replaying_causation_for_messages_deposited_in_bulk_should_clear_dispatch_state();
        else
            scenario.When_replaying_causation_on_outbox_should_clear_dispatch_state();

        return Task.CompletedTask;
    }

    private sealed class SharedStoreScenario(InMemoryOutbox outbox)
        : CausationTrackingOutboxBaseTests<CommittableTransaction>
    {
        protected override IAmAnOutboxSync<Message, CommittableTransaction> Outbox => outbox;
    }
}
