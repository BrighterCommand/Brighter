using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.MessageScheduler.TickerQ;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;
using ParamoreBrighter.TickerQ.Tests;
using Polly;
using Polly.Registry;
using TickerQ.DependencyInjection;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models.Ticker;

namespace Paramore.Brighter.TickerQ.Tests
{
    public class TickerQSchedulerMessageTests:IClassFixture<TickerQTestFixture>
    {
        private readonly TickerQTestFixture _fixture;

        public TickerQSchedulerMessageTests(TickerQTestFixture tickerQTestFixture)
        {
            _fixture = tickerQTestFixture;
        }

        [Fact]
        public async Task When_scheduler_a_message_with_a_datetimeoffset_async()
        {
            var req = new MyEvent();
            var message =
                new Message(
                    new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _fixture.RoutingKey },
                    new MessageBody(JsonSerializer.Serialize(req)));

            var scheduler = (IAmAMessageSchedulerAsync)_fixture.SchedulerFactory.Create(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(message,
                _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));
            var persist = _fixture.ServiceProvider.GetRequiredService<ITickerPersistenceProvider<TimeTicker,CronTicker>>();
            Assert.NotEqual(0, id.Length);

            Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey));

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.Equivalent(message, _fixture.Outbox.Get(message.Id, new RequestContext()));

            Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
        }
    }
}
