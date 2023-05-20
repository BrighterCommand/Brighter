namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAnOutbox
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into an OutBox to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// We provide implementations of <see cref="IAmAnOutboxSync{T}"/> for various databases. Users using other databases should consider a Pull Request
    /// </summary>
    /// <typeparam name="TMessage">The type of the message</typeparam>
    /// <typeparam name="TTransaction">The type of the database transaction</typeparam>
    public interface IAmAnOutbox<TMessage, TTransaction> where TMessage : Message
    {
        
    }
}
