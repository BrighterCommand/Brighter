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
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Paramore.Brighter.Test.Generator.Generators;

var factory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = factory.CreateLogger<Program>();

var configurationFileOptions = new Option<string>("--file")
{
    Description = "Path to the test configuration JSON file", 
    DefaultValueFactory = _ => "test-configuration.json",
    Required = false,
};

var command = new RootCommand("Generates test code for Brighter shared and outbox components from a configuration file");
command.Options.Add(configurationFileOptions);

var commandParser = command.Parse(args);
if (commandParser.Errors.Count > 0)
{
    logger.LogCritical("Error during parse the command options. {Errors}",
        string.Join(" ", commandParser.Errors.Select(x => x.Message)));
    return -1;
}

var configurationFile = commandParser.GetRequiredValue(configurationFileOptions);

if (!File.Exists(configurationFile))
{
    logger.LogInformation("The configuration file path {Path} is a directory, skipping it", configurationFile);
    return 0;
}

try
{
    await using var fs = File.OpenRead(configurationFile);
    var configuration = JsonSerializer.Deserialize<TestConfiguration>(fs);
    if (configuration == null)
    {
        logger.LogCritical("The configuration file {Path} could not be deserialized", configurationFile);
        return -1;
    }

    if (string.IsNullOrEmpty(configuration.DestinationFolder))
    {
        configuration.DestinationFolder = Directory.GetCurrentDirectory();
        logger.LogInformation("No destination folder specified, going to use {Folder}", configuration.DestinationFolder);
    }

    await new SharedGenerator(factory.CreateLogger<SharedGenerator>()).GenerateAsync(configuration);
    await new OutboxGenerator(factory.CreateLogger<OutboxGenerator>()).GenerateAsync(configuration);

    return 0;
}
catch (Exception e)
{
    logger.LogCritical(e, "An error occurred during the generation process");
    throw;
}
