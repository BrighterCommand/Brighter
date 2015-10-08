// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ianp
// Created          : 25-03-2014
//
// Last Modified By : ian
// Last Modified On : 25-03-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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

using Nancy.TinyIoc;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.Handlers;

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration
{
    public class DependencyResolver
    {
        internal static void ConfigureDependencies(TinyIoCContainer container)
        {
            var logger = LogProvider.GetLogger("MainNancyModule");
            logger.Log(LogLevel.Debug, () => "GET on messages");

            var messageStore =
                new MsSqlMessageStore(
                    new MsSqlMessageStoreConfiguration("Server=.;Database=brighterMessageStore;Trusted_Connection=True",
                        "messages", MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer), logger);

            container.Register(messageStore);
            container.Register(typeof (IMessageStoreConfigProvider), typeof (MessageStoreConfigProvider));
            container.Register(typeof (IMessageStoreViewerFactory), typeof (MessageStoreViewerFactory));
            container.Register(typeof(IHandleCommand<RepostCommand>), typeof (RepostCommandHandler));
            container.Register(typeof (IAmAMessageRecoverer), typeof (MessageRecoverer));

            var factory= new HandlerFactory();
            factory.Add(container.Resolve<RepostCommandHandler>());
            container.Register<IHandlerFactory>(factory);
        }
    }
}
