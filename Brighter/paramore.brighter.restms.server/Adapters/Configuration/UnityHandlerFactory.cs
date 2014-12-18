using System;
using System.ComponentModel;
using Microsoft.Practices.Unity;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.server.Adapters.Configuration
{
    public class UnityHandlerFactory : IAmAHandlerFactory
    {
        readonly UnityContainer container;

        public UnityHandlerFactory(UnityContainer container)
        {
            this.container = container;
        }

        /// <summary>
        /// Creates the specified handler type.
        /// </summary>
        /// <param name="handlerType">Type of the handler.</param>
        /// <returns>IHandleRequests.</returns>
        public IHandleRequests Create(Type handlerType)
        {
            return (IHandleRequests)container.Resolve(handlerType);
        }

        /// <summary>
        /// Releases the specified handler.
        /// </summary>
        /// <param name="handler">The handler.</param>
        public void Release(IHandleRequests handler)
        {
            var disposable = handler as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }
}
