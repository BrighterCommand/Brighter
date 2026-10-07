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

using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.GeneratedFileAudit;

/// <summary>
/// One audit of this repository's own tree, shared by the tests that read it.
/// </summary>
/// <remarks>
/// Orphans and missing files are two questions about the same audit, asked by two tests. Reading
/// every configuration and walking the tree twice to answer them would say the same thing twice
/// and cost twice as much.
/// </remarks>
public sealed class RepositoryTreeAudit
{
    /// <summary>
    /// The audit of the repository's <c>tests</c> folder.
    /// </summary>
    public GeneratedTreeAudit Audit { get; } = GeneratedTreeAudit.Of(GeneratedTreeAudit.LocateTestsRoot());
}

/// <summary>
/// Groups the tests that read the repository's own audit, so that they share one.
/// </summary>
[CollectionDefinition(NAME)]
public sealed class RepositoryTreeAuditCollection : ICollectionFixture<RepositoryTreeAudit>
{
    /// <summary>
    /// The collection name the tests sharing the audit are attributed with.
    /// </summary>
    public const string NAME = "Repository tree audit";
}
