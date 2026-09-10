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
/// <para>
/// The parser used to return an empty dictionary when it could not find the matrix header. That is
/// not "no criteria found": <c>GetSkip</c> answers the empty string for a missing row, and the empty
/// string means <em>run, with no Skip</em>. So a reformatted header row silently switched the whole
/// deferral mechanism off, and the suite reported every deferred behaviour as running.
/// </para>
/// <para>
/// The cross-check audit would have caught it downstream, by reporting every ledger key unresolved.
/// These tests exist because that is defence-in-depth doing the primary check's job - the same
/// argument <see cref="ConformanceLedger.LoadFrom"/> already makes for a ledger that cannot be found
/// at all.
/// </para>
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
        // Arrange - a ledger whose matrix header has been reformatted past recognition
        var path = ALedgerContaining(
            "# Conformance status\n\n| Configuration | Something | Else |\n|---|---|---|\n| Redis | Pass | Pass |\n");

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new ConformanceLedger(path));

        Assert.Contains("conformance matrix", exception.Message);
    }

    [Fact]
    public void When_the_matrix_holds_no_rows_should_throw()
    {
        // Arrange - a header the parser recognises, with nothing under it
        var path = ALedgerContaining(
            "# Conformance status\n\n| Configuration | FR-2 | FR-4 |\n|---|---|---|\n\nSome prose.\n");

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new ConformanceLedger(path));

        Assert.Contains("no data rows", exception.Message);
    }

    [Fact]
    public void When_a_column_name_only_appears_as_a_substring_should_not_match_the_header()
    {
        // Arrange - FR-22 contains "FR-2" as a substring. A header carrying FR-22 and FR-4 but no
        // FR-2 column is not the matrix, and matching it would read every cell from the wrong
        // column offsets rather than failing.
        var path = ALedgerContaining(
            "# Conformance status\n\n| Configuration | FR-22 | FR-4 |\n|---|---|---|\n| Redis | Pass | Pass |\n");

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new ConformanceLedger(path));

        Assert.Contains("conformance matrix", exception.Message);
    }

    [Fact]
    public void When_the_ledger_is_well_formed_should_parse_it()
    {
        // Arrange - guards against "make it throw" being satisfied by throwing unconditionally
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
