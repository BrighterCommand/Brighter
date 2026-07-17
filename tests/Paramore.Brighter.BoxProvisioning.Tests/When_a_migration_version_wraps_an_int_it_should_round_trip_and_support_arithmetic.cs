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

public class MigrationVersionRoundTripTests
{
    [Test]
    public async Task When_a_migration_version_is_created_from_an_int_it_should_expose_the_value()
    {
        //Arrange
        MigrationVersion v = 3;

        //Act + Assert
        await Assert.That(v.Value).IsEqualTo(3);
    }

    [Test]
    public async Task When_a_migration_version_is_implicitly_converted_to_int_it_should_yield_the_original()
    {
        //Arrange
        MigrationVersion v = 3;

        //Act
        int i = v;

        //Assert
        await Assert.That(i).IsEqualTo(3);
    }

    [Test]
    public async Task When_to_string_is_called_on_a_migration_version_it_should_return_the_number()
    {
        //Arrange
        MigrationVersion v = new(3);

        //Act + Assert
        await Assert.That(v.ToString()).IsEqualTo("3");
    }

    [Test]
    public async Task When_two_migration_versions_have_the_same_int_they_should_be_equal()
    {
        //Arrange
        var a = new MigrationVersion(3);
        var b = (MigrationVersion)3;

        //Act + Assert
        await Assert.That(b).IsEqualTo(a);
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task When_migration_version_participates_in_int_arithmetic_it_should_yield_the_correct_result()
    {
        //Arrange — FR-4: arithmetic via implicit → int
        var prev = (MigrationVersion)1;

        //Act
        int next = prev + 1;

        //Assert
        await Assert.That(next).IsEqualTo(2);
    }

    [Test]
    public async Task When_comparing_two_migration_versions_the_lower_value_should_be_less_than_the_higher()
    {
        //Arrange
        var lower = (MigrationVersion)1;
        var higher = (MigrationVersion)2;

        //Act + Assert
        await Assert.That(lower < higher).IsTrue();
        await Assert.That(higher < lower).IsFalse();
    }

    [Test]
    public async Task When_compare_to_is_called_it_should_order_by_value()
    {
        //Arrange
        var v1 = new MigrationVersion(1);
        var v2 = new MigrationVersion(2);
        var v1b = new MigrationVersion(1);

        //Act + Assert
        await Assert.That(v1.CompareTo(v2) < 0).IsTrue();
        await Assert.That(v2.CompareTo(v1) > 0).IsTrue();
        await Assert.That(v1.CompareTo(v1b)).IsEqualTo(0);
    }
}