using System;
using System.Collections.Generic;
using paramore.brighter.commandprocessor.messageviewer.Ports.Handlers;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    public class FakeHandlerFactory : IHandlerFactory
    {
        private Dictionary<string, object> handlers = new Dictionary<string, object>();

        public IHandleCommand<T> GetHandler<T>() where T : class, messageviewer.Ports.Handlers.ICommand
        {
            var fullName = typeof (T).FullName;
            if (!handlers.ContainsKey(fullName))
            {
                throw new ArgumentException("Cann't find handler for type " + fullName);
            }
            return (IHandleCommand<T>)handlers[fullName];
        }

        public void Add<T>(IHandleCommand<T> fakeHandler)
        {
            handlers.Add(typeof(T).FullName, fakeHandler);
        }
    }
}