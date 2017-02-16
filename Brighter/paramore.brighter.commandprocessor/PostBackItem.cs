using System.Threading;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Used with the <see cref="MessagePumpSynchronizationContext"/> this class stores the callback to be executed on completion
    /// of an asynchronous task (either via ContinueWith or use of async..await). We use it so that the message pump can provide
    /// a single threaded apartment, ensuring that the callback is always on the same thread. This is useful as it allows us
    /// to ensure that the pipeline for a message is guaranteed not to have concurrency concerns.
    /// </summary>
    public class PostBackItem
    {
        private readonly SendOrPostCallback _sendOrPostCallback;
        private readonly object _postbackState;

        /// <summary>
        /// Creates an instance of PostBackItem
        /// </summary>
        /// <param name="sendOrPostCallback">The callback delegate</param>
        /// <param name="postbackState">The arguments to pass to <see cref="sendOrPostCallback"/></param>
        public PostBackItem(SendOrPostCallback sendOrPostCallback, object postbackState)
        {
            _sendOrPostCallback = sendOrPostCallback;
            _postbackState = postbackState;
        }


        public void Call()
        {
            _sendOrPostCallback(_postbackState);
        }
    }
}
