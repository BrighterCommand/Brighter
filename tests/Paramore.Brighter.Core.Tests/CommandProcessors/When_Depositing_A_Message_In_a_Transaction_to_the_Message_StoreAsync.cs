using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    
    [Collection("CommandProcessor")]
    public class CommandProcessorDepositPostTransactionTestsAsync: IDisposable
    {
        
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message1;
        private readonly MyCommand _myCommand2 = new MyCommand();
        private readonly Message _message2;
        private readonly FakeOutboxSync _fakeOutboxSync;
        private readonly FakeMessageProducerWithPublishConfirmation _fakeMessageProducerWithPublishConfirmation;

        private readonly IAmABoxTransactionConnectionProvider txnProvider1 = new TxnProvider1();
        private readonly string txnProvider1Name = "provider1";
        private readonly IAmABoxTransactionConnectionProvider txnProvider2 = new TxnProvider2();
        private readonly string txnProvider2Name = "provider2";

        public CommandProcessorDepositPostTransactionTestsAsync()
        {
            _myCommand.Value = "Hello World";
            _myCommand2.Value = "Hello again.";

            _fakeOutboxSync = new FakeOutboxSync();
            _fakeMessageProducerWithPublishConfirmation = new FakeMessageProducerWithPublishConfirmation();

            var topic = "MyCommand";
            _message1 = new Message(
                new MessageHeader(_myCommand.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );
            
            _message2 = new Message(
                new MessageHeader(_myCommand2.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand2, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

            var boxTxnProviderRegistry =
                new BoxTransactionConnectionProviderRegistry(txnProvider1Name, txnProvider1).AddProvider(
                    txnProvider2Name, txnProvider2);
            
            PolicyRegistry policyRegistry = new PolicyRegistry { { CommandProcessor.RETRYPOLICYASYNC, retryPolicy }, { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy } };
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                messageMapperRegistry,
                _fakeOutboxSync,
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>() {{topic, _fakeMessageProducerWithPublishConfirmation},}),
                boxTransactionConnectionProviderRegistry: boxTxnProviderRegistry);
        }


        [Fact]
        public async Task When_depositing_a_message_in_the_outbox()
        {
            //act
            var postedMessageId = await _commandProcessor.DepositPostAsync(_myCommand);
            var postedMessage2Id =
                await _commandProcessor.DepositPostAsync(_myCommand2, transactionProviderName: txnProvider2Name);
            
            //assert
            //message should not be posted
            _fakeMessageProducerWithPublishConfirmation.MessageWasSent.Should().BeFalse();
            
            //message should be in the store
            var depositedPost = _fakeOutboxSync
                .OutstandingMessages(0)
                .SingleOrDefault(msg => msg.Id == _message1.Id);
            
            var depositedPost2 = _fakeOutboxSync
                .OutstandingMessages(0)
                .SingleOrDefault(msg => msg.Id == _message2.Id);

            depositedPost.Should().NotBeNull();
           
            //message should correspond to the command
            depositedPost.Id.Should().Be(_message1.Id);
            depositedPost.Body.Value.Should().Be(_message1.Body.Value);
            depositedPost.Header.Topic.Should().Be(_message1.Header.Topic);
            depositedPost.Header.MessageType.Should().Be(_message1.Header.MessageType);
            
            depositedPost2.Id.Should().Be(_message2.Id);
            depositedPost2.Body.Value.Should().Be(_message2.Body.Value);
            depositedPost2.Header.Topic.Should().Be(_message2.Header.Topic);
            depositedPost2.Header.MessageType.Should().Be(_message2.Header.MessageType);
            
            //message should be marked as outstanding if not sent
            var outstandingMessages = await _fakeOutboxSync.OutstandingMessagesAsync(0);
            var outstandingMessage = outstandingMessages.First(m => m.Id == postedMessageId);
            var outstandingMessage2 = outstandingMessages.First(m => m.Id == postedMessage2Id);
            outstandingMessage.Id.Should().Be(_message1.Id);
            outstandingMessage2.Id.Should().Be(_message2.Id);

            var depositedPostProvider1 = _fakeOutboxSync.TransactionProviderUsedForPost[postedMessageId];
            var depositedPostProvider2 = _fakeOutboxSync.TransactionProviderUsedForPost[postedMessage2Id];
            
            Assert.Equal(typeof(TxnProvider1), depositedPostProvider1);
            Assert.Equal(typeof(TxnProvider2), depositedPostProvider2);
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearExtServiceBus();
        }
     }

    public class TxnProvider1 : IAmABoxTransactionConnectionProvider
    {
        
    }
    
    public class TxnProvider2 : IAmABoxTransactionConnectionProvider
    {
        
    }
}
