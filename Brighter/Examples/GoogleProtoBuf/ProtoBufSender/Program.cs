using System;
using System.Threading;
using Brighter.Example.Messages;
using Google.Protobuf.Collections;
using ProtoBufSender.Configuration;
using SimpleInjector;


namespace ProtoBufSender
{
    class Program
    {
        static void Main(string[] args)
        {
            var container = new Container();

            var commandProcessor = BrighterConfig.Register(container);

            container.Verify();


            Random rnd = new Random();

            do
            {
                SampleEvent e = new SampleEvent
                {
                     EventIdentifier = rnd.Next(),
                     Summary = "Complex Event Sample"
                };
                e.Messages.Add(new SampleEvent.Types.EventPayload { Message = string.Format("Message {0}", rnd.Next()) });
                e.Messages.Add(new SampleEvent.Types.EventPayload { Message = string.Format("Message {0}", rnd.Next()) });
                e.Messages.Add(new SampleEvent.Types.EventPayload { Message = string.Format("Message {0}", rnd.Next()) });
                e.Messages.Add(new SampleEvent.Types.EventPayload { Message = string.Format("Message {0}", rnd.Next()) });

                commandProcessor.Post(e);


                SampleCommand cmd = new SampleCommand
                {
                    CommandParameter = string.Format("Command #{0}", rnd.Next()),
                    CommandValue = 3.141f,
                };

                commandProcessor.Post(cmd);

                Thread.Sleep(2000);
            } while (true);

        }
    }
}
