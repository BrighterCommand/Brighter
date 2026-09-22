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

using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.GeneratedFileAudit;

/// <summary>
/// The other direction. A generation code path that is dropped, or a project that is never
/// regenerated after the generator changes, leaves files the configuration asks for absent from
/// the tree - and absent tests raise no alarm of their own, because nothing runs to notice.
/// </summary>
[Collection(RepositoryTreeAuditCollection.NAME)]
public class GeneratedTreeMissingFileAuditTests(RepositoryTreeAudit repository)
{
    [Fact]
    public void When_auditing_the_generated_tree_should_find_no_missing_files()
    {
        // Arrange
        var audit = repository.Audit;

        // Act
        var missing = audit.Missing;

        // Assert - an audit that walked nothing would satisfy the emptiness below trivially, so
        // pin that it found a tree to read before reading its answer
        Assert.NotEmpty(audit.OnDisk);

        // Assert - every file the generator would write is on disk
        Assert.True(missing.Count == 0,
            "File(s) the current configuration would produce that are absent from the tree. " +
            "Run ./generate-test.sh and commit the result:\n" +
            string.Join("\n", missing.Select(file => $"  {file}")));
    }
}
