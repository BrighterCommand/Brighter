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

using Common.Logging;
using Polly;

namespace paramore.brighter.commandprocessor
{
    public class CommandProcessorBuilder : INeedAnIOCContainer, INeedALogging, INeedMessaging, INeedARequestContext, IAmACommandProcessorBuilder
    {
        private IAdaptAnInversionOfControlContainer container;
        private CommandProcessorBuilder() {}

        public static INeedAnIOCContainer With()
        {
            return new CommandProcessorBuilder();
        }

        public INeedALogging InversionOfControl(IAdaptAnInversionOfControlContainer theContainer)
        {
            this.container = theContainer;
            return this;
        }

        public INeedMessaging WithLogger(ILog logger)
        {
            container.Register<ILog, ILog>(logger);
            return this;
        }

        public INeedARequestContext WithMessaging(MessagingConfiguration configuration)
        {
            container.Register<IAmAMessageStore<Message>, IAmAMessageStore<Message>>(configuration.MessageStore);
            container.Register<IAmAMessagingGateway, IAmAMessagingGateway>(configuration.MessagingGateway);
            container.Register<Policy>(CommandProcessor.RETRYPOLICY, configuration.RetryPolicy);
            container.Register<Policy>(CommandProcessor.CIRCUITBREAKER, configuration.RetryPolicy);
            return this;
        }

        public IAmACommandProcessorBuilder WithRequestContextFactory(IAmARequestContextFactory requestContextFactory)
        {
            container.Register<IAmARequestContextFactory, IAmARequestContextFactory>(requestContextFactory);
            return this;
        }

        public CommandProcessor Build()
        {
            return new CommandProcessorFactory(container).Create();
        }
    }

    #region Progressive interfaces
    public interface INeedAnIOCContainer
    {
        INeedALogging InversionOfControl(IAdaptAnInversionOfControlContainer theContainer);
    }

    public interface INeedALogging
    {
        INeedMessaging WithLogger(ILog logger);
    }

    public interface INeedMessaging
    {
        INeedARequestContext WithMessaging(MessagingConfiguration configuration);
    }

    public interface INeedARequestContext
    {
        IAmACommandProcessorBuilder WithRequestContextFactory(IAmARequestContextFactory requestContextFactory);
    }
    public interface IAmACommandProcessorBuilder
    {
        CommandProcessor Build();
    }
    #endregion
}