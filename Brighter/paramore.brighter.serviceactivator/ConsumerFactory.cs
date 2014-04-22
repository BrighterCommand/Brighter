using System;
using System.Linq;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator
{
    internal class ConsumerFactory
    {
        private ConsumerFactory() {}

        public static Consumer Create(IAdaptAnInversionOfControlContainer container, Connection connection)
        {
            var messagePumpType = typeof (MessagePump<>).MakeGenericType(connection.DataType);
            var parameters = messagePumpType.GetConstructors()[0].GetParameters()
                                                    .Select(param => container.GetInstance(param.ParameterType))
                                                    .ToArray();
            var messagePump = (IAmAMessagePump) Activator.CreateInstance(messagePumpType, parameters);
            messagePump.Channel = connection.Channel;
            messagePump.TimeoutInMilliseconds = connection.TimeoutInMiliseconds;
            var lamp = new Consumer(connection.Channel, messagePump);
            return lamp;
        }
    }
}