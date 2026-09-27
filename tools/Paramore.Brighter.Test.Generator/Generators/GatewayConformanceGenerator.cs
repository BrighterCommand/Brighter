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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

/// <summary>
/// Generates the gateway channel factory conformance sweep test from a Liquid template based on
/// <see cref="GatewayConformanceConfiguration"/>.
/// </summary>
/// <param name="logger">The logger instance used for diagnostic output during generation.</param>
public class GatewayConformanceGenerator(ILogger<GatewayConformanceGenerator> logger)
    : BaseGenerator(logger)
{
    /// <summary>
    /// Generates the gateway conformance test file for <see cref="TestConfiguration.GatewayConformance"/>,
    /// if configured.
    /// </summary>
    /// <param name="configuration">The root test configuration containing the gateway conformance settings and destination folder.</param>
    public async Task GenerateAsync(TestConfiguration configuration)
    {
        foreach (var suite in Suites(configuration))
        {
            await GenerateAsync(
                configuration,
                suite.Prefix,
                suite.TemplateFolderName,
                suite.Model,
                suite.Ignore
            );
        }
    }

    /// <summary>
    /// Computes the file <see cref="GenerateAsync(TestConfiguration)"/> would write for
    /// <paramref name="configuration"/>, without writing it.
    /// </summary>
    /// <param name="configuration">The root test configuration containing the gateway conformance settings and destination folder.</param>
    /// <returns>The gateway conformance test file this configuration owns, or none if not configured.</returns>
    public IReadOnlyList<PlannedFile> Plan(TestConfiguration configuration)
    {
        var planned = new List<PlannedFile>();
        foreach (var suite in Suites(configuration))
        {
            planned.AddRange(Plan(configuration, suite.Prefix, suite.TemplateFolderName, suite.Ignore));
        }

        return planned;
    }

    /// <summary>
    /// Describes every rendering this generator performs for <paramref name="configuration"/>: one
    /// sweep per project, not per gateway variant, so there is at most one suite regardless of how
    /// many <see cref="MessagingGatewayConfiguration"/> variants the project also configures.
    /// </summary>
    /// <remarks>
    /// Both <see cref="GenerateAsync(TestConfiguration)"/> and <see cref="Plan(TestConfiguration)"/>
    /// walk this list, so the file the generator writes and the file it claims to own are one
    /// description rather than two that can drift apart.
    /// </remarks>
    private IReadOnlyList<GenerationSuite> Suites(TestConfiguration configuration)
    {
        if (configuration.GatewayConformance == null)
        {
            logger.LogInformation("No gateway conformance configured");
            return Array.Empty<GenerationSuite>();
        }

        var model = configuration.GatewayConformance.WithDefaultsFrom(configuration);

        return new[]
        {
            new GenerationSuite(
                Path.Combine("MessagingGateway", "Generated", "Conformance"),
                "GatewayConformance",
                model)
        };
    }
}
