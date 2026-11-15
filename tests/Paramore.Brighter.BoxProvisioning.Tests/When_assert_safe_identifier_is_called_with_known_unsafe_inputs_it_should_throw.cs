using System.Threading.Tasks;
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


namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// <see cref="Identifiers.AssertSafe"/> rejects identifiers that are unsafe to interpolate into
/// DDL unescaped, accepts plain ones, and reports a null identifier as missing configuration.
/// </summary>
public class AssertSafeIdentifierTests
{
    [Test]
    [Arguments("O'Brien")]              // single quote — breaks an information_schema probe
    [Arguments("Outbox; DROP")]         // semicolon — statement terminator
    [Arguments("my-outbox")]            // hyphen — invalid in bare identifiers
    [Arguments("1Outbox")]              // leading digit — invalid as bare identifier across all backends
    [Arguments("")]                     // empty
    [Arguments("_underscore_leading")]  // leading underscore — Spanner reserves `_`-prefixed identifiers
    public async Task When_assert_safe_identifier_is_called_with_known_unsafe_inputs_it_should_throw(string unsafeIdentifier)
    {
        //Arrange
        const string parameterName = "tableName";

        //Act
        var exception = await Assert.That(() => Identifiers.AssertSafe(unsafeIdentifier, parameterName)).ThrowsExactly<ConfigurationException>();

        //Assert
        await Assert.That(exception.Message).Contains(parameterName);
        await Assert.That(exception.Message).Contains(unsafeIdentifier);
    }

    [Test]
    [Arguments("Outbox")]
    [Arguments("my_outbox_v2")]
    [Arguments("Outbox123")]
    public void When_assert_safe_identifier_is_called_with_safe_inputs_it_should_not_throw(string safeIdentifier)
    {
        //Act + Assert
        Identifiers.AssertSafe(safeIdentifier, "tableName");
    }

    [Test]
    public async Task When_assert_safe_identifier_is_called_with_a_null_identifier_it_should_throw()
    {
        //Act
        var exception = await Assert.That(() => Identifiers.AssertSafe(null!, "tableName")).ThrowsExactly<ConfigurationException>();

        //Assert
        await Assert.That(exception.Message).Contains("tableName");
    }

    [Test]
    public async Task When_assert_safe_identifier_is_called_with_a_null_identifier_it_should_not_blame_the_regex()
    {
        //Act
        var exception = await Assert.That(() => Identifiers.AssertSafe(null!, "tableName")).ThrowsExactly<ConfigurationException>();

        //Assert
        await Assert.That(exception.Message).DoesNotContain("regex");
        await Assert.That(exception.Message).Contains("null");
    }
}
