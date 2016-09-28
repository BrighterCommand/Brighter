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
using System.Threading.Tasks;
using NUnit.Specifications;
using nUnitShouldAdapter;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.MessageDispatch.TestDoubles;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageDispatch
{
    public class When_Running_A_Message_Pump_On_A_Thread_Should_Be_Able_To_Stop : NUnit.Specifications.ContextSpecification
    {
        private static Performer s_performer;
        private static SpyCommandProcessor s_commandProcessor;
        private static FakeChannel s_channel;
        private static Task s_performerTask;

        private Establish _context = () =>
        {
            s_commandProcessor = new SpyCommandProcessor();
            s_channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            var messagePump = new MessagePump<MyEvent>(s_commandProcessor, mapper);
            messagePump.Channel = s_channel;
            messagePump.TimeoutInMilliseconds = 5000;

            var @event = new MyEvent();
            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(@event)));
            s_channel.Add(message);

            s_performer = new Performer(s_channel, messagePump);
            s_performerTask = s_performer.Run();
            s_performer.Stop();
        };

        private Because _of = () => s_performerTask.Wait();
        private It _should_terminate_successfully = () => s_performerTask.IsCompleted.ShouldBeTrue();
        private It _should_not_have_errored = () => s_performerTask.IsFaulted.ShouldBeFalse();
        private It _should_not_show_as_cancelled = () => s_performerTask.IsCanceled.ShouldBeFalse();
        private It _should_have_consumed_the_messages_in_the_channel = () => s_channel.Length.ShouldEqual(0);
    }
}
