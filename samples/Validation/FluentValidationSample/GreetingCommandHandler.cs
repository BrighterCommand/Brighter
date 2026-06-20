#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using Paramore.Brighter;
using Paramore.Brighter.RequestValidation.Attributes;

namespace FluentValidationSample;

/// <summary>
/// The business handler for <see cref="GreetingCommand"/>. The <see cref="ValidateRequestAttribute"/> runs the
/// validation handler first, so this method only executes once the request is known to be valid.
/// </summary>
public sealed class GreetingCommandHandler : RequestHandler<GreetingCommand>
{
    [ValidateRequest(step: 1, timing: HandlerTiming.Before)]
    public override GreetingCommand Handle(GreetingCommand command)
    {
        Console.WriteLine($"  Handler ran: Hello {command.Name} <{command.Email}>");

        return base.Handle(command);
    }
}
