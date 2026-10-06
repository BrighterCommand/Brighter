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
/// The generator writes files and never removes one it previously wrote. A flipped capability
/// flag, a renamed or deleted template, or a dropped generation code path therefore leaves output
/// on disk that still compiles and still runs, while no longer being anything the configuration
/// asks for. This audit is what notices.
/// </summary>
[Collection(RepositoryTreeAuditCollection.NAME)]
public class GeneratedTreeOrphanAuditTests(RepositoryTreeAudit repository)
{
    [Fact]
    public void When_auditing_the_generated_tree_should_find_no_orphans()
    {
        // Arrange
        var audit = repository.Audit;

        // Act
        var orphans = audit.Orphans;

        // Assert - an audit that planned nothing would satisfy the emptiness below trivially, so
        // pin that it found work to do before reading its answer
        Assert.NotEmpty(audit.Expected);

        // Assert - every file under a Generated/ directory is one the generator would write
        Assert.True(orphans.Count == 0,
            "File(s) under a Generated/ directory that the current configuration would not " +
            "produce. The generator never deletes, so these have to go by hand - confirm the " +
            "configuration is right first, because an orphan says configuration and tree " +
            "disagree, not which of them is wrong:\n" +
            string.Join("\n", orphans.Select(orphan => $"  {orphan}")));
    }
}
