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

public class SqlScriptRoundTripTests
{
    [Test]
    public async Task When_a_sql_script_is_created_from_a_string_it_should_expose_the_value()
    {
        //Arrange
        SqlScript script = "ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL";

        //Act + Assert
        await Assert.That(script.Value).IsEqualTo("ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL");
    }

    [Test]
    public async Task When_a_sql_script_is_implicitly_converted_to_string_it_should_yield_the_original()
    {
        //Arrange
        SqlScript script = "ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL";

        //Act
        string? sql = script;

        //Assert
        await Assert.That(sql).IsEqualTo("ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL");
    }

    [Test]
    public async Task When_to_string_is_called_on_a_sql_script_it_should_return_the_value()
    {
        //Arrange
        SqlScript script = new("ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL");

        //Act + Assert
        await Assert.That(script.ToString()).IsEqualTo("ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL");
    }

    [Test]
    public async Task When_two_sql_scripts_have_the_same_string_they_should_be_equal()
    {
        //Arrange
        var a = new SqlScript("ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL");
        var b = (SqlScript)"ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL";

        //Act + Assert
        await Assert.That(b).IsEqualTo(a);
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task When_sql_script_is_used_as_nullable_null_should_be_legal()
    {
        //Arrange — null idempotency-check script is a valid V1 migration value (AC-3, FR-6)
        SqlScript? guard = null;

        //Act
        string? sql = guard;

        //Assert
        await Assert.That(guard).IsNull();
        await Assert.That(sql).IsNull();
    }

    [Test]
    public async Task When_sql_script_wraps_arbitrary_text_it_should_not_validate_content()
    {
        //Arrange — FR-6: type does not validate or reject content
        const string arbitrary = "DROP TABLE --;/* anything */";

        //Act — must not throw
        SqlScript script = arbitrary;

        //Assert
        await Assert.That(script.Value).IsEqualTo(arbitrary);
    }

    [Test]
    public async Task When_sql_script_is_null_or_empty_is_called_with_null_it_should_return_true()
    {
        //Act + Assert
        await Assert.That(SqlScript.IsNullOrEmpty(null)).IsTrue();
    }

    [Test]
    public async Task When_sql_script_is_null_or_empty_is_called_with_empty_string_it_should_return_true()
    {
        //Arrange
        var empty = (SqlScript)"";

        //Act + Assert
        await Assert.That(SqlScript.IsNullOrEmpty(empty)).IsTrue();
    }

    [Test]
    public async Task When_sql_script_is_null_or_empty_is_called_with_non_empty_string_it_should_return_false()
    {
        //Arrange
        var script = (SqlScript)"ALTER TABLE Outbox ADD Source NVARCHAR(255) NULL";

        //Act + Assert
        await Assert.That(SqlScript.IsNullOrEmpty(script)).IsFalse();
    }
}