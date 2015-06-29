using System;
using System.Collections.Generic;

namespace paramore.brighter.commandprocessor.messageviewer.Ports.Handlers
{
    public class HandlerFactory : IHandlerFactory
    {
        private readonly Dictionary<string, object> _handlers = new Dictionary<string, object>();

        public IHandleCommand<T> GetHandler<T>() where T : class, ICommand
        {
            var fullName = typeof (T).FullName;
            if (!_handlers.ContainsKey(fullName))
            {
                throw new ArgumentException("Cann't find handler for type " + fullName);
            }
            return (IHandleCommand<T>)_handlers[fullName];
        }


        public void Add<T>(IHandleCommand<T> fakeHandler)
        {
            _handlers.Add(typeof(T).FullName, fakeHandler);
        }
    }
}