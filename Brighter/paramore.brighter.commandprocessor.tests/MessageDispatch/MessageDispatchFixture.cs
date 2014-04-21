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
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.MessageDispatcher.TestDoubles;

namespace paramore.commandprocessor.tests.MessageDispatcher
{
    public class When_a_message_dispatcher_is_asked_to_connect_a_channel_and_handler
    {
        static Dispatcher dispatcher;
        static IAmAMessageChannel channel;
        static IAmACommandProcessor commandProcessor;

        Establish context = () =>
            {
                var container = new TinyIoCAdapter(new TinyIoCContainer());
                channel = new InMemoryChannel();
                commandProcessor = new SpyCommandProcessor();

                var logger = A.Fake<ILog>();
                container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
                container.Register<IAmAMessageMapper<MyEvent>, MyEventMessageMapper>();

                var plug = new Plug(cord: channel, jack: typeof(MyEvent), noOfPeformers: 1, timeoutInMiliseconds: 1000);
                dispatcher = new Dispatcher(container, new List<Plug>{plug}, logger);

                var @event = new MyEvent();
                var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(@event)));
                channel.Enqueue(message);

                dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
                dispatcher.Recieve();
            };


            Because of = () =>
                {
                    Task.Delay(1000).Wait();
                    dispatcher.End().Wait();
                };

            It should_have_consumed_the_messages_in_the_channel = () => channel.Length.ShouldEqual(0);
            It should_have_a_stopped_state = () => dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
    }
}
