using System;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Azure.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public class TopicClientWrapper : ITopicClient
    {
        private readonly TopicClient _topicClient;

        public TopicClientWrapper(TopicClient topicClient)
        {
            _topicClient = topicClient;
        }

        public void Send(Microsoft.Azure.ServiceBus.Message message)
        {
            //Azure Service Bus only supports IsolationLevel.Serializable if in a transaction
            if (Transaction.Current != null && Transaction.Current.IsolationLevel != IsolationLevel.Serializable)
            {
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    _topicClient.SendAsync(message).Wait();
                }
            }
            else
            {
                _topicClient.SendAsync(message).Wait();
            }
        }

        public async Task SendAsync(Microsoft.Azure.ServiceBus.Message message)
        {
            //Azure Service Bus only supports IsolationLevel.Serializable if in a transaction
            if (Transaction.Current != null && Transaction.Current.IsolationLevel != IsolationLevel.Serializable)
            {
                using (new TransactionScope(TransactionScopeOption.Suppress))
                {
                    await _topicClient.SendAsync(message);
                }
            }
            else
            {
                await _topicClient.SendAsync(message);
            }
        }

        public void ScheduleMessage(Microsoft.Azure.ServiceBus.Message message, DateTimeOffset scheduleEnqueueTime)
        {
            _topicClient.ScheduleMessageAsync(message, scheduleEnqueueTime).Wait();
        }

        public async Task ScheduleMessageAsync(Microsoft.Azure.ServiceBus.Message message, DateTimeOffset scheduleEnqueueTime)
        {
            await _topicClient.ScheduleMessageAsync(message, scheduleEnqueueTime);
        }

        public void Close()
        {
            _topicClient.CloseAsync().Wait();
        }

        public async Task CloseAsync()
        {
            await _topicClient.CloseAsync();
        }
    }
}
