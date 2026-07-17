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

public class SchemaNameRoundTripTests
{
    [Test]
    public async Task When_a_schema_name_is_created_from_a_string_it_should_expose_the_value()
    {
        //Arrange
        SchemaName sn = "dbo";

        //Act + Assert
        await Assert.That(sn.Value).IsEqualTo("dbo");
    }

    [Test]
    public async Task When_a_schema_name_is_implicitly_converted_to_string_it_should_yield_the_original()
    {
        //Arrange
        SchemaName sn = "dbo";

        //Act
        string? s = sn;

        //Assert
        await Assert.That(s).IsEqualTo("dbo");
    }

    [Test]
    public async Task When_to_string_is_called_on_a_schema_name_it_should_return_the_value()
    {
        //Arrange
        SchemaName sn = new("dbo");

        //Act + Assert
        await Assert.That(sn.ToString()).IsEqualTo("dbo");
    }

    [Test]
    public async Task When_two_schema_names_have_the_same_string_they_should_be_equal()
    {
        //Arrange
        var a = new SchemaName("dbo");
        var b = (SchemaName)"dbo";

        //Act + Assert
        await Assert.That(b).IsEqualTo(a);
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task When_schema_name_is_used_as_nullable_null_should_be_legal()
    {
        //Arrange — SQLite passes no schema; null is a legitimate value (AC-3, FR-2)
        SchemaName? sn = null;

        //Act
        string? s = sn;

        //Assert
        await Assert.That(sn).IsNull();
        await Assert.That(s).IsNull();
    }

    [Test]
    public async Task When_schema_name_is_null_or_empty_is_called_with_null_it_should_return_true()
    {
        //Act + Assert
        await Assert.That(SchemaName.IsNullOrEmpty(null)).IsTrue();
    }

    [Test]
    public async Task When_schema_name_is_null_or_empty_is_called_with_empty_string_it_should_return_true()
    {
        //Arrange
        var empty = (SchemaName)"";

        //Act + Assert
        await Assert.That(SchemaName.IsNullOrEmpty(empty)).IsTrue();
    }

    [Test]
    public async Task When_schema_name_is_null_or_empty_is_called_with_non_empty_string_it_should_return_false()
    {
        //Arrange
        var sn = (SchemaName)"dbo";

        //Act + Assert
        await Assert.That(SchemaName.IsNullOrEmpty(sn)).IsFalse();
    }
}