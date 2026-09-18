namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAHandlerFactory
    /// This is the Interface type for <see cref="IAmAHandlerFactorySync"/> and <see cref="IAmAHandlerFactoryAsync"/>
    /// </summary>
    public interface IAmAHandlerFactory
    {
        /// <summary>
        /// Creates a DI scope for one handler pipeline to resolve from, or null when this factory has
        /// none to offer. The caller must always release the returned handle; releasing it may or may
        /// not dispose an underlying scope, and the handle alone knows which.
        /// </summary>
        IAmAScope? CreatePipelineScope();
    }
}
