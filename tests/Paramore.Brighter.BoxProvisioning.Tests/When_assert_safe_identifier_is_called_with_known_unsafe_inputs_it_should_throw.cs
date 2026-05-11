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

using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

// Item Q (spec 0027 PR #4039 third review). Identifiers.AssertSafe is a defence-in-depth
// helper that runs at *MigrationCatalog.All(...) factory entry and at runner MigrateAsync entry to
// reject SQL identifiers that fall outside the regex ^[A-Za-z_][A-Za-z0-9_]*$. The point of
// this regex is not "what does the backend allow" — quoted identifiers in MSSQL/PG/Spanner
// (and backticked identifiers in MySQL) accept much more — but "what is safe to interpolate
// into ALTER TABLE / CREATE TABLE strings without escaping". Spec 0027 has not yet shipped,
// so the runtime rejection of unusual identifiers does not break any released configuration.
//
// This test exercises the helper directly with the canonical unsafe inputs called out in the
// reviewer comment: a single quote (the actual injection vector at MySqlOutboxMigrationCatalog.cs:165
// where the table name is inlined into an information_schema probe), a semicolon (statement
// separator), a hyphen (rejected outside backticks/brackets), a leading digit (rejected by every
// backend's bare-identifier rule), and the empty string (degenerate).

public class AssertSafeIdentifierTests
{
    [Theory]
    [InlineData("O'Brien")]      // single quote — breaks information_schema probe at MySqlOutboxMigrationCatalog.cs:165
    [InlineData("Outbox; DROP")] // semicolon — statement terminator
    [InlineData("my-outbox")]    // hyphen — invalid in bare identifiers
    [InlineData("1Outbox")]      // leading digit — invalid as bare identifier across all backends
    [InlineData("")]             // empty
    public void When_assert_safe_identifier_is_called_with_known_unsafe_inputs_it_should_throw(string unsafeIdentifier)
    {
        //Arrange
        const string parameterName = "tableName";

        //Act
        var exception = Assert.Throws<ConfigurationException>(
            () => Identifiers.AssertSafe(unsafeIdentifier, parameterName));

        //Assert — message must name both the offending identifier (so operators see the bad value)
        // and the parameter (so contributors see which call-site rejected it).
        Assert.Contains(parameterName, exception.Message);
        Assert.Contains(unsafeIdentifier, exception.Message);
    }

    [Theory]
    [InlineData("Outbox")]
    [InlineData("my_outbox_v2")]
    [InlineData("_underscore_leading")]
    [InlineData("Outbox123")]
    public void When_assert_safe_identifier_is_called_with_safe_inputs_it_should_not_throw(string safeIdentifier)
    {
        //Act + Assert — the regex must accept canonical legal identifiers; an over-strict
        // helper would reject the very names the BrighterCommand defaults use.
        Identifiers.AssertSafe(safeIdentifier, "tableName");
    }

    [Fact]
    public void When_assert_safe_identifier_is_called_with_a_null_identifier_it_should_throw()
    {
        //Act
        var exception = Assert.Throws<ConfigurationException>(
            () => Identifiers.AssertSafe(null!, "tableName"));

        //Assert — null fails the same defence-in-depth check; the helper is the only place
        // a null leak would surface as an actionable error rather than a downstream NRE.
        Assert.Contains("tableName", exception.Message);
    }
}
