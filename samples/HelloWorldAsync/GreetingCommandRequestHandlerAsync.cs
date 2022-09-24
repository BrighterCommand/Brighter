#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;

namespace HelloWorldAsync
{
    internal class GreetingCommandRequestHandlerAsync : RequestHandlerAsync<GreetingCommand>
    {
        [RequestLoggingAsync(step: 1, timing: HandlerTiming.Before)]
        public override async Task<GreetingCommand> HandleAsync(GreetingCommand command, CancellationToken cancellationToken = default(CancellationToken))
        {
            var api = new IpFyApi(new Uri("https://api.ipify.org"));

            var result = await api.GetAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            Console.WriteLine("Hello {0}", command.Name);
            Console.WriteLine(result.Success ? "Your public IP address is {0}" : "Call to IpFy API failed : {0}", result.Message);

            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
    }
}
