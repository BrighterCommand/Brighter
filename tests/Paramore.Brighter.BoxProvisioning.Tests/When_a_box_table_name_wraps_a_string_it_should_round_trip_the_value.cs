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

public class BoxTableNameRoundTripTests
{
    [Test]
    public async Task When_a_box_table_name_is_created_from_a_string_it_should_expose_the_value()
    {
        //Arrange
        BoxTableName tableName = "Outbox";

        //Act + Assert
        await Assert.That(tableName.Value).IsEqualTo("Outbox");
    }

    [Test]
    public async Task When_a_box_table_name_is_implicitly_converted_to_string_it_should_yield_the_original()
    {
        //Arrange
        BoxTableName tableName = "Outbox";

        //Act
        string? s = tableName;

        //Assert
        await Assert.That(s).IsEqualTo("Outbox");
    }

    [Test]
    public async Task When_to_string_is_called_on_a_box_table_name_it_should_return_the_value()
    {
        //Arrange
        BoxTableName tableName = new("Outbox");

        //Act + Assert
        await Assert.That(tableName.ToString()).IsEqualTo("Outbox");
    }

    [Test]
    public async Task When_two_box_table_names_have_the_same_string_they_should_be_equal()
    {
        //Arrange
        var a = new BoxTableName("Outbox");
        var b = (BoxTableName)"Outbox";

        //Act + Assert
        await Assert.That(b).IsEqualTo(a);
        await Assert.That(a == b).IsTrue();
    }

    // NOTE: type isolation (FR-13, D1) — new BoxTableName("dbo") == new SchemaName("dbo")
    // must not compile; these are distinct record types with no common base.
    // This is a compile-time constraint, not an executable assertion.
}