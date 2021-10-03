using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAHandlerFactory
    /// This is the Interface type for <see cref="IAmAHandlerFactorySync"/> and <see cref="IAmAHandlerFactoryAsync"/>
    /// </summary>
    public interface IAmAHandlerFactory
    {
        IServiceScope CreateScope();
    }
}
