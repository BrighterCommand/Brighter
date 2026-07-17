using System;
using System.Collections.Generic;
using FakeItEasy;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Handlers;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class HeartbeatMessageTests
    {
        private const string TEST_FIRST_CONNECTION_NAME = "Test.First.Subscription";
        private const string TEST_SECOND_CONNECTION_NAME = "Test.Second.Subscription";
        private const string TEST_ROUTING_KEY = "test.routing.key";
        private readonly SpyCommandProcessor _spyCommandProcessor = new SpyCommandProcessor();
        private readonly string _correlationId = Guid.NewGuid().ToString();
        private readonly HeartbeatRequestCommandHandler _handler;
        private readonly HeartbeatRequest _heartbeatRequest;
        private readonly string _hostName;
        public HeartbeatMessageTests()
        {
            var dispatcher = A.Fake<IDispatcher>();
            var firstConsumer = A.Fake<IAmAConsumer>();
            var secondConsumer = A.Fake<IAmAConsumer>();
            A.CallTo(() => firstConsumer.Name).Returns(new ConsumerName(TEST_FIRST_CONNECTION_NAME));
            A.CallTo(() => firstConsumer.State).Returns(ConsumerState.Open);
            A.CallTo(() => secondConsumer.Name).Returns(new ConsumerName(TEST_SECOND_CONNECTION_NAME));
            A.CallTo(() => secondConsumer.State).Returns(ConsumerState.Shut);
            A.CallTo(() => dispatcher.Consumers).Returns(new List<IAmAConsumer> { firstConsumer, secondConsumer });
            var hostName = new HostName($"{Environment.MachineName}.{System.Reflection.Assembly.GetEntryAssembly()?.FullName}");
            A.CallTo(() => dispatcher.HostName).Returns(hostName);
            _hostName = hostName;
            _heartbeatRequest = new HeartbeatRequest(new ReplyAddress(TEST_ROUTING_KEY, _correlationId));
            _handler = new HeartbeatRequestCommandHandler(_spyCommandProcessor, dispatcher);
        }

        [Test]
        public async Task When_recieving_a_heartbeat_message()
        {
            _handler.Handle(_heartbeatRequest);
            // Should post back a heartbeat response
            await Assert.That(_spyCommandProcessor.WasCalled(CommandType.Post)).IsTrue();
            // Should have diagnostic information in the response
            var heartbeatEvent = _spyCommandProcessor.Observe<HeartbeatReply>();
            await Assert.That(heartbeatEvent.HostName).IsEqualTo(_hostName);
            await Assert.That(heartbeatEvent.SendersAddress.Topic).IsEqualTo(new RoutingKey(TEST_ROUTING_KEY));
            await Assert.That(heartbeatEvent.SendersAddress.CorrelationId.Value).IsEqualTo(_correlationId);
            await Assert.That(heartbeatEvent.Consumers[0].ConsumerName.ToString()).IsEqualTo(TEST_FIRST_CONNECTION_NAME);
            await Assert.That(heartbeatEvent.Consumers[0].State).IsEqualTo(ConsumerState.Open);
            await Assert.That(heartbeatEvent.Consumers[1].ConsumerName.ToString()).IsEqualTo(TEST_SECOND_CONNECTION_NAME);
            await Assert.That(heartbeatEvent.Consumers[1].State).IsEqualTo(ConsumerState.Shut);
        }
    }
}