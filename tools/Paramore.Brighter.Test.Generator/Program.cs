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
    Description = "<Add description>",
    DefaultValueFactory = _ => "test-configuration.json",
    Required = false,
};

var command = new RootCommand("<Add description>");
command.Options.Add(configurationFileOptions );

var commandParser = command.Parse(args);
if (commandParser.Errors.Count > 0)
{
    logger.LogCritical("Error during parse the commad options. {Errors}", string.Join(" ", commandParser.Errors.Select(x => x.Message)));
    return -1;
}

var configurationFile = commandParser.GetRequiredValue(configurationFileOptions);

if(!File.Exists(configurationFile))
{
    logger.LogCritical("The configuration file path {Path} is a directory", configurationFile);
    return 0;
}

await using var fs = File.OpenRead(configurationFile);
var configuration = JsonSerializer.Deserialize<TestConfigurationConfiguration>(fs)!;
if (string.IsNullOrEmpty(configuration.DestinyFolder))
{
    configuration.DestinyFolder = Directory.GetCurrentDirectory();
    logger.LogInformation("No destiny folder specified, going to use {Folder}",  configuration.DestinyFolder);
}

await new SharedGenerator(factory.CreateLogger<SharedGenerator>()).GenerateAsync(configuration);
await new OutboxGenerator(factory.CreateLogger<OutboxGenerator>()).GenerateAsync(configuration);

return 0;
