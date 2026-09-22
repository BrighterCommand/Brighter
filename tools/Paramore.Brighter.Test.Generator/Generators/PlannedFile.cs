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

namespace Paramore.Brighter.Test.Generator.Generators;

/// <summary>
/// One file a generator will write: the Liquid template that produces it, and where it lands.
/// </summary>
/// <remarks>
/// The generator writes files and never removes one it previously wrote, so the tree can hold
/// output the current configuration would no longer produce. Planning the output as data, rather
/// than only as a side effect of rendering, lets an audit compare what the generator owns with
/// what is on disk.
/// </remarks>
/// <param name="TemplatePath">The absolute path of the <c>.liquid</c> template that is rendered.</param>
/// <param name="DestinationPath">The absolute path the rendered file is written to.</param>
public sealed record PlannedFile(string TemplatePath, string DestinationPath);
