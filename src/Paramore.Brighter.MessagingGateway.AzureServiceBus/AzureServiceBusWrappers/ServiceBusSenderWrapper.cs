using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    internal sealed class ServiceBusSenderWrapper : IServiceBusSenderWrapper
    {
        private readonly ServiceBusSender _serviceBusSender;

        internal ServiceBusSenderWrapper(ServiceBusSender serviceBusSender)
        {
            _serviceBusSender = serviceBusSender;
        }

        public async Task SendAsync(ServiceBusMessage message,
            CancellationToken cancellationToken = default)
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

        public Task ScheduleMessageAsync(ServiceBusMessage message, DateTimeOffset scheduleEnqueueTime,
            CancellationToken cancellationToken = default) =>
            _serviceBusSender.ScheduleMessageAsync(message, scheduleEnqueueTime, cancellationToken);

        public ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(CancellationToken cancellationToken = default) 
            => _serviceBusSender.CreateMessageBatchAsync(cancellationToken);

        public Task SendAsync(ServiceBusMessageBatch batch, CancellationToken cancellationToken = default)
            => _serviceBusSender.SendMessagesAsync(batch, cancellationToken);

        public Task CloseAsync()
        {
            return _serviceBusSender.CloseAsync();
        }
    }
}
