using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

public abstract class BaseGenerator(ILogger logger)
{
    private readonly Parser _parser = new();
    
    protected virtual async Task GenerateAsync(TestConfigurationConfiguration configuration, 
        string prefix, 
        string templateFolderName,
        object model)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", templateFolderName);
        var fileNames = Directory.GetFiles(templatePath , "*.liquid", SearchOption.TopDirectoryOnly);
        
        logger.LogInformation("Founded {FileCount} liquid files", fileNames.Length);
        
        var destinyPath = Path.Combine(configuration.DestinyFolder, prefix);
        logger.LogInformation("Base destiny Path {FileCount}", destinyPath);

        foreach (var fileName in fileNames)
        {
            var file = new FileInfo(fileName);
            var destinyTemplateFile = new FileInfo(
                Path.Combine(destinyPath, file.Name.Replace(".liquid", string.Empty)));
            
            destinyTemplateFile.Directory!.Create();
            
            logger.LogInformation("Generating file from {TemplatePath} to {DestinyPath}", file.FullName, destinyTemplateFile.FullName);
            
            await _parser.ParseAsync(new ParseContext(file.FullName,
                destinyTemplateFile.FullName,
                model));
        }
    }
}
