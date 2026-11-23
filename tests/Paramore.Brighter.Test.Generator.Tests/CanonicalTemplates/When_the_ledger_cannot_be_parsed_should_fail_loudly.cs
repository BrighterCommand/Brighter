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
using Paramore.Brighter.Test.Generator;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// A ledger that is present but unreadable must fail the same way a missing one does.
/// </summary>
/// <remarks>
/// A ledger whose matrix cannot be read resolves every cell to the empty Skip, which means <em>run</em>, so the
/// whole deferral mechanism would silently switch off. A header that only carries a column name as a
/// substring (FR-22 for FR-2) is not the matrix. A well-formed fact guards against throwing unconditionally.
/// </remarks>
public class LedgerParseFailureTests : IDisposable
{
    private readonly string _testDirectory;

    public LedgerParseFailureTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LedgerParseTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    private string ALedgerContaining(string markdown)
    {
        var path = Path.Combine(_testDirectory, "conformance-status.md");
        File.WriteAllText(path, markdown);
        return path;
    }

    [Fact]
    public void When_the_matrix_header_cannot_be_found_should_throw()
    {
        // Arrange
        var path = ALedgerContaining(
            "# Conformance status\n\n| Configuration | Something | Else |\n|---|---|---|\n| Redis | Pass | Pass |\n");

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new ConformanceLedger(path));

        Assert.Contains("conformance matrix", exception.Message);
    }

    [Fact]
    public void When_the_matrix_holds_no_rows_should_throw()
    {
        // Arrange
        var path = ALedgerContaining(
            "# Conformance status\n\n| Configuration | FR-2 | FR-4 |\n|---|---|---|\n\nSome prose.\n");

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new ConformanceLedger(path));

        Assert.Contains("no data rows", exception.Message);
    }

    [Fact]
    public void When_a_column_name_only_appears_as_a_substring_should_not_match_the_header()
    {
        // Arrange
        var path = ALedgerContaining(
            "# Conformance status\n\n| Configuration | FR-22 | FR-4 |\n|---|---|---|\n| Redis | Pass | Pass |\n");

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new ConformanceLedger(path));

        Assert.Contains("conformance matrix", exception.Message);
    }

    [Fact]
    public void When_the_ledger_is_well_formed_should_parse_it()
    {
        // Arrange
        var path = ALedgerContaining(
            "# Conformance status\n\n| Configuration | FR-2 | FR-4 |\n|---|---|---|\n| Redis / X | Pass | Pass |\n");

        // Act
        var ledger = new ConformanceLedger(path);

        // Assert
        Assert.True(ledger.HasRow("Redis / X"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
