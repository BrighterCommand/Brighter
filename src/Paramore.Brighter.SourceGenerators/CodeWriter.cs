#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.CodeDom.Compiler;
using System.IO;

namespace Paramore.Brighter.SourceGenerators;

/// <summary>
/// A thin <see cref="IndentedTextWriter"/> wrapper that manages brace blocks so emit code can
/// express structure (<see cref="StartBlock"/> / <see cref="EndBlock"/>) instead of hand-rolling
/// indentation whitespace. Modelled on the CodeWriter used by the ASP.NET Core source generators.
/// </summary>
internal sealed class CodeWriter : IndentedTextWriter
{
    public CodeWriter(StringWriter writer, int baseIndent = 0) : base(writer)
    {
        Indent = baseIndent;
    }

    /// <summary>Write an opening brace and increase the indent for the block body.</summary>
    public void StartBlock()
    {
        WriteLine("{");
        Indent++;
    }

    /// <summary>
    /// Decrease the indent and write a closing brace, optionally followed by <paramref name="suffix"/>
    /// — for example <c>");"</c> to close a method-call lambda block.
    /// </summary>
    public void EndBlock(string suffix = "")
    {
        Indent--;
        WriteLine("}" + suffix);
    }
}
