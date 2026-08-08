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

using FluentValidationSample;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.RequestValidation;
using Paramore.Brighter.Validation.FluentValidation;
using global::FluentValidation;

var builder = Host.CreateApplicationBuilder();

// FluentValidation resolves an IValidator<TRequest> from the container, so register one per validated request.
builder.Services.AddSingleton<IValidator<GreetingCommand>>(new GreetingCommandValidator());

// AutoFromAssemblies discovers GreetingCommandHandler in this assembly; UseFluentValidation() maps the
// provider-agnostic [ValidateRequest] attribute to the FluentValidation implementation.
builder.Services
    .AddBrighter()
    .AutoFromAssemblies()
    .UseFluentValidation();

var host = builder.Build();
var commandProcessor = host.Services.GetRequiredService<IAmACommandProcessor>();

Console.WriteLine("Sending a valid greeting...");
commandProcessor.Send(new GreetingCommand { Name = "Ada", Email = "ada@example.com" });

Console.WriteLine();
Console.WriteLine("Sending an invalid greeting (empty name and email)...");
try
{
    commandProcessor.Send(new GreetingCommand { Name = "", Email = "" });
}
catch (RequestValidationException exception)
{
    Console.WriteLine($"Rejected before the handler ran, with {exception.Errors.Count} error(s):");
    foreach (var error in exception.Errors)
        Console.WriteLine($"  - {error.PropertyName}: {error.ErrorMessage}");
}
