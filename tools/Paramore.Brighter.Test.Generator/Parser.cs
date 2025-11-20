using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fluid;

namespace Paramore.Brighter.Test.Generator;

public class Parser
{
    private readonly FluidParser _parser = new();
    
    public async Task ParseAsync(ParseContext context)
    {
        var template = await File.ReadAllTextAsync(context.SourceFilePath);
        var liquid = _parser.Parse(template);
        
        await using var fs = File.Create(context.DestinyFilePath);
        await using var writer = new StreamWriter(fs, Encoding.UTF8);
        await liquid.RenderAsync(writer, new TemplateContext(context.Model));
    }
}

public record ParseContext(string SourceFilePath, string DestinyFilePath, object Model);
