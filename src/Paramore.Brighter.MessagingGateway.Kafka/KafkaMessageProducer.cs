using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    class KafkaMessageProducer : IAmAMessageProducerSupportingDelay, IAmAMessageProducerAsync
    {
        public bool DelaySupported => throw new NotImplementedException();

        public void Send(Message message)
        {
            throw new NotImplementedException();
        }

        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~KafkaMessageProducer() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        public Task SendAsync(Message message)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
