// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using paramore.brighter.commandprocessor;
using TinyIoC;

namespace Tasklist.Adapters
{
    internal class TinyIocHandlerFactory : IAmAHandlerFactory
    {
        private readonly TinyIoCContainer _container;

        public TinyIocHandlerFactory(TinyIoCContainer container)
        {
            _container = container;
        }

        public IHandleRequests Create(Type handlerType)
        {
            return (IHandleRequests)_container.Resolve(handlerType);
        }

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
