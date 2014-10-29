#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Linq;
using System.Reflection;
using Common.Logging;

namespace paramore.brighter.commandprocessor
{
    public abstract class RequestHandler<TRequest> : IHandleRequests<TRequest> where TRequest : class, IRequest
    {
        protected readonly ILog logger;
        private IHandleRequests<TRequest> _successor;

        protected RequestHandler(ILog logger)
        {
            this.logger = logger;
        }

        public IRequestContext Context {get; set; }

        public HandlerName Name
        {
            get { return new HandlerName(GetType().Name); }
        }

        public IHandleRequests<TRequest> Successor
        {
            set { _successor = value; }
        }

        public void AddToLifetime(IAmALifetime instanceScope)
        {
            if (this is IDisposable)
                instanceScope.Add(this);

            if (_successor != null)
                _successor.AddToLifetime(instanceScope);

        }

        public void DescribePath(IAmAPipelineTracer pathExplorer)
        {
            pathExplorer.AddToPath(Name);
            if (_successor != null)
                _successor.DescribePath(pathExplorer);
        }

        public virtual TRequest Handle(TRequest command)
        {
            if (_successor != null)
            {
                logger.Debug(m => m("Passing request from {0} to {1}", Name, _successor.Name));
                return _successor.Handle(command);
            }

            return command;
        }

            //default is just to do nothing - use this if you need to pass data from an attribute into a handler
        public virtual void InitializeFromAttributeParams(params object[] initializerList) {}


        internal MethodInfo FindHandlerMethod()
        {
            var methods = GetType().GetMethods();
            return methods
                .Where(method => method.Name == "Handle")
                .Where(method => method.GetParameters().Count() == 1 && method.GetParameters().Single().ParameterType == typeof(TRequest))
                .SingleOrDefault();
        }
    }
}
