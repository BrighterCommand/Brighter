using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    internal class ServiceBusSenderWrapper : IServiceBusSenderWrapper
    {
        private readonly ServiceBusSender _serviceBusSender;

        internal ServiceBusSenderWrapper(ServiceBusSender serviceBusSender)
        {
            _serviceBusSender = serviceBusSender;
        }

        public void Send(ServiceBusMessage message)
        {
            //Azure Service Bus only supports IsolationLevel.Serializable if in a transaction
            if (Transaction.Current != null && Transaction.Current.IsolationLevel != IsolationLevel.Serializable)
            {
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    _serviceBusSender.SendMessageAsync(message).Wait();
                }
            }
            else
            {
                _serviceBusSender.SendMessageAsync(message).Wait();
            }
        }

        public async Task SendAsync(ServiceBusMessage message,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            //Azure Service Bus only supports IsolationLevel.Serializable if in a transaction
            if (Transaction.Current != null && Transaction.Current.IsolationLevel != IsolationLevel.Serializable)
            {
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    await _serviceBusSender.SendMessageAsync(message, cancellationToken);
                }
            }
            else
            {
                await _serviceBusSender.SendMessageAsync(message, cancellationToken);
            }
        }

        public Task SendAsync(ServiceBusMessage[] messages, CancellationToken cancellationToken = default(CancellationToken))
        {
            return _serviceBusSender.SendMessagesAsync(messages, cancellationToken);
        }

        public void ScheduleMessage(ServiceBusMessage message, DateTimeOffset scheduleEnqueueTime)
        {
            _serviceBusSender.ScheduleMessageAsync(message, scheduleEnqueueTime).Wait();
        }

        public async Task ScheduleMessageAsync(ServiceBusMessage message, DateTimeOffset scheduleEnqueueTime,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            await _serviceBusSender.ScheduleMessageAsync(message, scheduleEnqueueTime, cancellationToken);
        }

        public void Close()
        {
            _serviceBusSender.CloseAsync().Wait();
        }

        public async Task CloseAsync()
        {
            await _serviceBusSender.CloseAsync();
        }
    }
}
