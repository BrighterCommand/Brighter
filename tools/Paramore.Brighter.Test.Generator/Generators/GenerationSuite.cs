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

namespace Paramore.Brighter.Test.Generator.Generators;

/// <summary>
/// One template-folder-to-destination-folder rendering that a generator performs for a
/// configuration: a suite of tests such as the Reactor variant of one messaging gateway, or the
/// Sync variant of one outbox.
/// </summary>
/// <remarks>
/// A generator describes its work as a list of these once, so that generating the files and
/// planning them walk the same description and cannot disagree about which files the generator owns.
/// </remarks>
/// <param name="Prefix">
/// The relative path appended to <see cref="Configuration.TestConfiguration.DestinationFolder"/>
/// to give the output directory for this suite.
/// </param>
/// <param name="TemplateFolderName">
/// The subfolder within the Templates directory holding this suite's Liquid templates.
/// </param>
/// <param name="Model">The model object whose properties the templates read while rendering.</param>
/// <param name="Ignore">
/// An optional predicate that, when returning <c>true</c> for a template file name, leaves that
/// template out of the suite.
/// </param>
public sealed record GenerationSuite(
    string Prefix,
    string TemplateFolderName,
    object Model,
    Func<string, bool>? Ignore = null);
