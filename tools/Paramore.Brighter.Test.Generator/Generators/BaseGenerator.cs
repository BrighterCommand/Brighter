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
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

/// <summary>
/// Base class for test code generators that render Liquid templates from a template folder into a destination directory.
/// </summary>
/// <param name="logger">The logger instance used for diagnostic output during generation.</param>
public abstract class BaseGenerator(ILogger logger)
{
    private readonly Parser _parser = new();

    /// <summary>
    /// Generates test files by rendering all Liquid templates in the specified template folder into the destination directory.
    /// </summary>
    /// <param name="configuration">The root test configuration containing the destination folder and shared settings.</param>
    /// <param name="prefix">A relative path prefix appended to <see cref="TestConfiguration.DestinationFolder"/> for the output directory.</param>
    /// <param name="templateFolderName">The name of the subfolder within the Templates directory that contains the Liquid templates.</param>
    /// <param name="model">The model object whose properties are available to the templates during rendering.</param>
    /// <param name="ignore">An optional predicate that, when returning <c>true</c> for a template file name, causes that template to be skipped.</param>
    protected virtual async Task GenerateAsync(TestConfiguration configuration, 
        string prefix, string templateFolderName,
        object model, Func<string, bool>? ignore = null)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", templateFolderName);
        var fileNames = Directory.GetFiles(templatePath , "*.liquid", SearchOption.TopDirectoryOnly);
        
        logger.LogInformation("Found {FileCount} liquid files", fileNames.Length);
        
        var destinationFolder = Path.Combine(configuration.DestinationFolder, prefix);
        logger.LogInformation("Base destination folder {FileCount}", destinationFolder);
        
        ignore  ??= _ => false;

        foreach (var fileName in fileNames)
        {
            if (ignore(fileName))
            {
                logger.LogInformation("Skipping {FileName}", fileName);
                continue;
            }
            
            var file = new FileInfo(fileName);
            var destinationTemplateFile = new FileInfo(Path.Combine(destinationFolder, file.Name.Replace(".liquid", string.Empty)));
            if (destinationTemplateFile.Directory == null)
            {
                logger.LogError("Destination folder {DestinationFolder} does not exist", destinationFolder);
                continue;
            }
            
            destinationTemplateFile.Directory.Create();
            
            logger.LogInformation("Generating file from {TemplatePath} to {DestinationPath}", file.FullName, destinationTemplateFile.FullName);
            
            await _parser.ParseAsync(new ParseContext(file.FullName,
                destinationTemplateFile.FullName,
                model));
        }
    }
}
