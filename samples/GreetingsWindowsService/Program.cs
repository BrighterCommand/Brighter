using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Topshelf;

namespace GreetingsWindowsService
{
    public class Program
    {
        public static void Main(string[] args)
        {            /*
             * Send a message in this format to this service and it will print it out
             * We document this here so that you can simply paste this into the RMQ web portal
             * to see commands flowing through the system. 
             * {"Greeting":"hello world","Id":"0a81cbbc-5f82-4912-99ee-19f0b7ee4bc8"}
             */

            HostFactory.Run(x => x.Service<GreetingService>(sc =>
            {
                sc.ConstructUsing(() => new GreetingService());

                // the start and stop methods for the service
                sc.WhenStarted((s, hostcontrol) => s.Start(hostcontrol));
                sc.WhenStopped((s, hostcontrol) => s.Stop(hostcontrol));

                // optional, when shutdown is supported
                sc.WhenShutdown((s, hostcontrol) => s.Shutdown(hostcontrol));
            }));
        }
    }
}
