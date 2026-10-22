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

/// <summary>
/// <see cref="Identifiers.AssertSafe"/> rejects identifiers that are unsafe to interpolate into
/// DDL unescaped, accepts plain ones, and reports a null identifier as missing configuration.
/// </summary>
public class AssertSafeIdentifierTests
{
    [Theory]
    [InlineData("O'Brien")]              // single quote — breaks an information_schema probe
    [InlineData("Outbox; DROP")]         // semicolon — statement terminator
    [InlineData("my-outbox")]            // hyphen — invalid in bare identifiers
    [InlineData("1Outbox")]              // leading digit — invalid as bare identifier across all backends
    [InlineData("")]                     // empty
    [InlineData("_underscore_leading")]  // leading underscore — Spanner reserves `_`-prefixed identifiers
    public void When_assert_safe_identifier_is_called_with_known_unsafe_inputs_it_should_throw(string unsafeIdentifier)
    {
        //Arrange
        const string parameterName = "tableName";

        //Act
        var exception = Assert.Throws<ConfigurationException>(
            () => Identifiers.AssertSafe(unsafeIdentifier, parameterName));

        //Assert
        Assert.Contains(parameterName, exception.Message);
        Assert.Contains(unsafeIdentifier, exception.Message);
    }

    [Theory]
    [InlineData("Outbox")]
    [InlineData("my_outbox_v2")]
    [InlineData("Outbox123")]
    public void When_assert_safe_identifier_is_called_with_safe_inputs_it_should_not_throw(string safeIdentifier)
    {
        //Act + Assert
        Identifiers.AssertSafe(safeIdentifier, "tableName");
    }

    [Fact]
    public void When_assert_safe_identifier_is_called_with_a_null_identifier_it_should_throw()
    {
        //Act
        var exception = Assert.Throws<ConfigurationException>(
            () => Identifiers.AssertSafe(null!, "tableName"));

        //Assert
        Assert.Contains("tableName", exception.Message);
    }

    [Fact]
    public void When_assert_safe_identifier_is_called_with_a_null_identifier_it_should_not_blame_the_regex()
    {
        //Act
        var exception = Assert.Throws<ConfigurationException>(
            () => Identifiers.AssertSafe(null!, "tableName"));

        //Assert
        Assert.DoesNotContain("regex", exception.Message);
        Assert.Contains("null", exception.Message);
    }
}
