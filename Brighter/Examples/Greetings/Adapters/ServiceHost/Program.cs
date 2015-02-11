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

using Topshelf;

namespace Greetings.Adapters.ServiceHost
{
    class Program
    {
        public static void Main()
        {
            /*
             * Send a message in this format to this service and it will print it out
             * We document this here so that you can simply paste this into the RMQ web portal
             * to see commands flowing through the system. 
             * {"Greeting":"hello world","Id":"0a81cbbc-5f82-4912-99ee-19f0b7ee4bc8"}
             */

            HostFactory.Run(x => x.Service<GreetingService >(sc =>
                {
                    sc.ConstructUsing(() => new GreetingService ());

                    // the start and stop methods for the service
                    sc.WhenStarted((s, hostcontrol) => s.Start(hostcontrol));
                    sc.WhenStopped((s, hostcontrol) => s.Stop(hostcontrol));

                    // optional, when shutdown is supported
                    sc.WhenShutdown((s, hostcontrol) => s.Shutdown(hostcontrol));
                }));
        }
    }
}
