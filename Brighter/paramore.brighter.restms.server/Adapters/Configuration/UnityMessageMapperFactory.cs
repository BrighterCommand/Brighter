using System;
using Microsoft.Practices.Unity;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.server.Adapters.Configuration
{
    public class UnityMessageMapperFactory  : IAmAMessageMapperFactory
    {
        readonly UnityContainer container;

        public UnityMessageMapperFactory(UnityContainer container)
        {
            this.container = container;
        }

        /// <summary>
        /// Creates the specified message mapper type.
        /// </summary>
        /// <param name="messageMapperType">Type of the message mapper.</param>
        /// <returns>IAmAMessageMapper.</returns>
        public IAmAMessageMapper Create(Type messageMapperType)
        {
              return (IAmAMessageMapper) container.Resolve(messageMapperType);
        }

    }
}
