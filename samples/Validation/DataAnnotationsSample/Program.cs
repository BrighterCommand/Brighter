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

using DataAnnotationsSample;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.RequestValidation;
using Paramore.Brighter.Validation.DataAnnotations;

var builder = Host.CreateApplicationBuilder();

// DataAnnotations needs nothing extra registered: the constraints live on the request type itself.
// AutoFromAssemblies discovers RegisterUserHandler; UseDataAnnotations() maps the provider-agnostic
// [ValidateRequest] attribute to the DataAnnotations implementation.
builder.Services
    .AddBrighter()
    .AutoFromAssemblies()
    .UseDataAnnotations();

var host = builder.Build();
var commandProcessor = host.Services.GetRequiredService<IAmACommandProcessor>();

Console.WriteLine("Sending a valid registration...");
commandProcessor.Send(new RegisterUser { Name = "Ada", Email = "ada@example.com" });

Console.WriteLine();
Console.WriteLine("Sending an invalid registration (empty name, malformed email)...");
try
{
    commandProcessor.Send(new RegisterUser { Name = "", Email = "not-an-email" });
}
catch (RequestValidationException exception)
{
    Console.WriteLine($"Rejected before the handler ran, with {exception.Errors.Count} error(s):");
    foreach (var error in exception.Errors)
        Console.WriteLine($"  - {error.PropertyName}: {error.ErrorMessage}");
}
