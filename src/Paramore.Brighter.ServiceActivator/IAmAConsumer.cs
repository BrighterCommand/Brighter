using System;
using System.Threading.Tasks;

namespace Paramore.Brighter.ServiceActivator
{
    public interface IAmAConsumer : IDisposable
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        ConsumerName Name { get; set; }

        /// <summary>
        /// Gets the performer.
        /// </summary>
        /// <value>The performer.</value>
        IAmAPerformer Performer { get; }

        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        ConsumerState State { get; set; }

        /// <summary>
        /// Gets or sets the job.
        /// </summary>
        /// <value>The job.</value>
        Task Job { get; set; }

        int JobId { get; set; }

        /// <summary>
        /// Opens the task queue and begin receiving messages.
        /// </summary>
        void Open();

        /// <summary>
        /// Shuts the task, which will not receive messages.
        /// </summary>
        void Shut();
    }
}