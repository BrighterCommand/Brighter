using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.ServiceActivator
{
    public interface IAmAConsumer : IDisposable
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        ConnectionName Name { get; }

        /// <summary>
        /// Gets the performer.
        /// </summary>
        /// <value>The performer.</value>
        IAmAPerformer Performer { get; }

        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        ConsumerState State { get; }

        /// <summary>
        /// Opens the task queue and begin receiving messages.
        /// </summary>
        Task Open(CancellationToken cancellationToken);

        /// <summary>
        /// Shuts the task, which will not receive messages.
        /// </summary>
        void Shut();
    }
}