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

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fluid;

namespace Paramore.Brighter.Test.Generator;

/// <summary>
/// Parses Liquid templates and renders them to output files using the Fluid template engine.
/// </summary>
public class Parser
{
    private readonly FluidParser _parser = new();

    /// <summary>
    /// Parses a Liquid template file and renders it to the destination file using the provided model.
    /// </summary>
    /// <param name="context">The <see cref="ParseContext"/> containing the source template path, destination path, and model data.</param>
    public async Task ParseAsync(ParseContext context)
    {
        var template = await File.ReadAllTextAsync(context.SourceFilePath);
        var liquid = _parser.Parse(template);
        
        await using var fs = File.Create(context.DestinationFilePath);
        await using var writer = new StreamWriter(fs, Encoding.UTF8);
        await liquid.RenderAsync(writer, new TemplateContext(context.Model));
    }
}

/// <summary>
/// Represents the context for a template parsing operation, containing source, destination, and model information.
/// </summary>
/// <param name="SourceFilePath">The path to the Liquid template source file.</param>
/// <param name="DestinationFilePath">The path where the rendered output file will be written.</param>
/// <param name="Model">The model object whose properties are available to the template during rendering.</param>
public record ParseContext(string SourceFilePath, string DestinationFilePath, object Model);
