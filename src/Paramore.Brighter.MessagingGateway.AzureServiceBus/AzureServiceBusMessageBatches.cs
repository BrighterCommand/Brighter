using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// Manages adding messages to batches
    /// </summary>
    /// <param name="sender">service bus sender used to determine batch size in bytes</param>
    /// <param name="routingKey">routing key representing a queue/topic</param>
    public class AzureServiceBusMessageBatches
        (IServiceBusSenderWrapper sender, RoutingKey routingKey) : List<IAmAMessageBatch>
    {
        private AzureServiceBusMessageBatch? _currentBatch;

        /// <summary>
        /// Adds message to a AzureServiceBusMessageBatch with a fallback to AzureServiceBusSingleMessageBatch
        /// </summary>
        /// <param name="message">message to add to the batch</param>
        /// <param name="cancellationToken">cancellation token</param>
        /// <returns></returns>
        public async Task AddMessageToBatch(Message message, CancellationToken cancellationToken)
        {
            if (!await TryAddToAzureServiceBusMessageBatch(message, cancellationToken))
            {
                Add(AzureServiceBusSingleMessageBatch.CreateBatch(message));
            }
        }

        private async Task<bool> TryAddToAzureServiceBusMessageBatch(Message message, CancellationToken cancellationToken)
        {
            _currentBatch ??= await CreateAzureServiceBusMessageBatch(cancellationToken);
            
            if (_currentBatch.TryAddMessage(message))
            {
                return true;
            }

            if (_currentBatch.IsEmpty)
            {
                return false;
            }

            _currentBatch = await CreateAzureServiceBusMessageBatch(cancellationToken);

            return _currentBatch.TryAddMessage(message);
        }

        private async Task<AzureServiceBusMessageBatch> CreateAzureServiceBusMessageBatch(CancellationToken cancellationToken)
        {
            var batch =  await AzureServiceBusMessageBatch.CreateBatch(sender, routingKey, cancellationToken);
            
            Add(batch);

            return batch;
        }

        public IEnumerable<IAmAMessageBatch> Batches 
            => this.Where(x => x.Ids().Any());
    }
}
