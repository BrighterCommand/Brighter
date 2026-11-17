#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox;

[Trait("Category", "InMemory")]
public class InMemoryOutstandingCountOutboxTests : OutstandingCountOutboxBaseTests<CommittableTransaction>
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTimeOffset _countAt;
    private readonly InMemoryOutbox _outbox;

    public InMemoryOutstandingCountOutboxTests()
    {
        _timeProvider = new FakeTimeProvider();
        _countAt = _timeProvider.GetUtcNow() + TimeSpan.FromDays(366);
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = new BrighterTracer() };
    }

    protected override IAmAnOutboxAsync<Message, CommittableTransaction> Outbox => _outbox;

    protected override async Task AddMessageWithAgeAsync(Message message, TimeSpan age)
    {
        _timeProvider.SetUtcNow(_countAt - age);
        await _outbox.AddAsync(message, new RequestContext());
    }

    protected override void BeforeCount()
    {
        _timeProvider.SetUtcNow(_countAt);
    }

    [Fact]
    public async Task When_counting_outstanding_messages_older_than_the_age_should_count_only_stale_ones()
    {
        await AssertAgeAsync();
    }

    [Fact]
    public async Task When_counting_outstanding_messages_with_a_ceiling_of_three_should_return_three()
    {
        await AssertCeilingOfThreeAsync();
    }

    [Fact]
    public async Task When_counting_outstanding_messages_with_a_ceiling_of_five_should_return_five()
    {
        await AssertCeilingOfFiveAsync();
    }
}
