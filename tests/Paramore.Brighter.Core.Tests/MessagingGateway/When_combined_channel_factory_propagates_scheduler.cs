#region Licence
/* The MIT License (MIT)

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
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;

namespace Paramore.Brighter.Core.Tests.MessagingGateway;
public class CombinedChannelFactorySchedulerTests
{
    [Test]
    public async Task Should_propagate_scheduler_to_inner_factories()
    {
        // Arrange
        var factory1 = new SchedulerAwareMockFactory();
        var factory2 = new SchedulerAwareMockFactory();
        var combined = new CombinedChannelFactory([factory1, factory2]);
        var scheduler = A.Fake<IAmAMessageScheduler>();
        // Act
        ((IAmAChannelFactoryWithScheduler)combined).Scheduler = scheduler;
        // Assert
        await Assert.That(factory1.Scheduler).IsSameReferenceAs(scheduler);
        await Assert.That(factory2.Scheduler).IsSameReferenceAs(scheduler);
    }

    [Test]
    public async Task Should_read_scheduler_from_first_inner_factory()
    {
        // Arrange
        var scheduler = A.Fake<IAmAMessageScheduler>();
        var factory1 = new SchedulerAwareMockFactory
        {
            Scheduler = scheduler
        };
        var factory2 = new SchedulerAwareMockFactory();
        var combined = new CombinedChannelFactory([factory1, factory2]);
        // Act
        var result = ((IAmAChannelFactoryWithScheduler)combined).Scheduler;
        // Assert
        await Assert.That(result).IsSameReferenceAs(scheduler);
    }

    [Test]
    public async Task Should_skip_inner_factories_that_do_not_implement_scheduler_interface()
    {
        // Arrange
        var plainFactory = new PlainMockFactory();
        var schedulerFactory = new SchedulerAwareMockFactory();
        var combined = new CombinedChannelFactory([plainFactory, schedulerFactory]);
        var scheduler = A.Fake<IAmAMessageScheduler>();
        // Act
        ((IAmAChannelFactoryWithScheduler)combined).Scheduler = scheduler;
        // Assert — only the scheduler-aware factory gets the scheduler
        await Assert.That(schedulerFactory.Scheduler).IsSameReferenceAs(scheduler);
    }

    [Test]
    public async Task Should_implement_scheduler_interface()
    {
        var combined = new CombinedChannelFactory([]);
        await Assert.That(combined).IsAssignableTo<IAmAChannelFactoryWithScheduler>();
    }

    private class SchedulerAwareMockFactory : Paramore.Brighter.IAmAChannelFactory, Paramore.Brighter.IAmAChannelFactoryWithScheduler
    {
        public IAmAMessageScheduler? Scheduler { get; set; }

        public IAmAChannelSync CreateSyncChannel(Subscription subscription) => A.Fake<IAmAChannelSync>();
        public IAmAChannelAsync CreateAsyncChannel(Subscription subscription) => A.Fake<IAmAChannelAsync>();
        public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default) => Task.FromResult(A.Fake<IAmAChannelAsync>());
    }

    private class PlainMockFactory : IAmAChannelFactory
    {
        public IAmAChannelSync CreateSyncChannel(Subscription subscription) => A.Fake<IAmAChannelSync>();
        public IAmAChannelAsync CreateAsyncChannel(Subscription subscription) => A.Fake<IAmAChannelAsync>();
        public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default) => Task.FromResult(A.Fake<IAmAChannelAsync>());
    }
}