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

public class MigrationVersionRoundTripTests
{
    [Fact]
    public void When_a_migration_version_is_created_from_an_int_it_should_expose_the_value()
    {
        //Arrange
        MigrationVersion v = 3;

        //Act + Assert
        Assert.Equal(3, v.Value);
    }

    [Fact]
    public void When_a_migration_version_is_implicitly_converted_to_int_it_should_yield_the_original()
    {
        //Arrange
        MigrationVersion v = 3;

        //Act
        int i = v;

        //Assert
        Assert.Equal(3, i);
    }

    [Fact]
    public void When_to_string_is_called_on_a_migration_version_it_should_return_the_number()
    {
        //Arrange
        MigrationVersion v = new(3);

        //Act + Assert
        Assert.Equal("3", v.ToString());
    }

    [Fact]
    public void When_two_migration_versions_have_the_same_int_they_should_be_equal()
    {
        //Arrange
        var a = new MigrationVersion(3);
        var b = (MigrationVersion)3;

        //Act + Assert
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void When_migration_version_participates_in_int_arithmetic_it_should_yield_the_correct_result()
    {
        //Arrange — FR-4: arithmetic via implicit → int
        var prev = (MigrationVersion)1;

        //Act
        int next = prev + 1;

        //Assert
        Assert.Equal(2, next);
    }

    [Fact]
    public void When_comparing_two_migration_versions_the_lower_value_should_be_less_than_the_higher()
    {
        //Arrange
        var lower = (MigrationVersion)1;
        var higher = (MigrationVersion)2;

        //Act + Assert
        Assert.True(lower < higher);
        Assert.False(higher < lower);
    }

    [Fact]
    public void When_compare_to_is_called_it_should_order_by_value()
    {
        //Arrange
        var v1 = new MigrationVersion(1);
        var v2 = new MigrationVersion(2);
        var v1b = new MigrationVersion(1);

        //Act + Assert
        Assert.True(v1.CompareTo(v2) < 0);
        Assert.True(v2.CompareTo(v1) > 0);
        Assert.Equal(0, v1.CompareTo(v1b));
    }
}
