#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

/// <summary>
/// Generates shared test infrastructure code (such as default message builders and assertions) from Liquid templates.
/// These shared files are rendered into the root of the destination folder and provide common helpers used by outbox and messaging gateway tests.
/// </summary>
/// <param name="logger">The logger instance used for diagnostic output during generation.</param>
public class SharedGenerator(ILogger<SharedGenerator> logger) : BaseGenerator(logger)
{
    /// <summary>
    /// Generates shared test infrastructure files, applying default values for
    /// <see cref="TestConfiguration.MessageBuilder"/> and <see cref="TestConfiguration.MessageAssertion"/> when not specified.
    /// </summary>
    /// <param name="configuration">The root test configuration containing shared settings and destination folder.</param>
    public async Task GenerateAsync(TestConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.MessageBuilder))
        {
            configuration.MessageBuilder = "DefaultMessageBuilder";
        }

        if (string.IsNullOrEmpty(configuration.MessageAssertion))
        {
            configuration.MessageAssertion = "DefaultMessageAssertion";
        }

        logger.LogInformation("Generating shared class for testing");
        await GenerateAsync(configuration, "", "", configuration);
    }
}
