using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{

    public class MessagePumpFailingMessageTranslationTestsAsync
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly FakeChannel _channel;

        public MessagePumpFailingMessageTranslationTestsAsync()
        {
            SpyRequeueCommandProcessor commandProcessor = new();
            var provider = new CommandProcessorProvider(commandProcessor);
            _channel = new FakeChannel();
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new FailingEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyFailingMapperEvent, FailingEventMessageMapperAsync>();
             
            _messagePump = new MessagePumpAsync<MyFailingMapperEvent>(provider, messageMapperRegistry, null)
            {
                Channel = _channel, TimeoutInMilliseconds = 5000, RequeueCount = 3, UnacceptableMessageLimit = 3
            };

            var unmappableMessage = new Message(new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_EVENT), new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }"));

            _channel.Enqueue(unmappableMessage);
        }

        [Fact]
        public async Task When_A_Message_Fails_To_Be_Mapped_To_A_Request ()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);

            _channel.Stop();

            await Task.WhenAll(task);

            //should_have_acknowledge_the_message
            _channel.AcknowledgeHappened.Should().BeTrue();
        }
    }
}
