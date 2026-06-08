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

public class MigrationDescriptionRoundTripTests
{
    [Fact]
    public void When_a_migration_description_is_created_from_a_string_it_should_expose_the_value()
    {
        //Arrange
        MigrationDescription d = "Add Source column";

        //Act + Assert
        Assert.Equal("Add Source column", d.Value);
    }

    [Fact]
    public void When_a_migration_description_is_implicitly_converted_to_string_it_should_yield_the_original()
    {
        //Arrange
        MigrationDescription d = "Add Source column";

        //Act
        string? s = d;

        //Assert
        Assert.Equal("Add Source column", s);
    }

    [Fact]
    public void When_to_string_is_called_on_a_migration_description_it_should_return_the_value()
    {
        //Arrange
        MigrationDescription d = new("Add Source column");

        //Act + Assert
        Assert.Equal("Add Source column", d.ToString());
    }

    [Fact]
    public void When_two_migration_descriptions_have_the_same_string_they_should_be_equal()
    {
        //Arrange
        var a = new MigrationDescription("Add Source column");
        var b = (MigrationDescription)"Add Source column";

        //Act + Assert
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void When_migration_description_is_null_or_empty_is_called_with_null_it_should_return_true()
    {
        //Act + Assert
        Assert.True(MigrationDescription.IsNullOrEmpty(null));
    }

    [Fact]
    public void When_migration_description_is_null_or_empty_is_called_with_empty_string_it_should_return_true()
    {
        //Arrange
        var empty = (MigrationDescription)"";

        //Act + Assert
        Assert.True(MigrationDescription.IsNullOrEmpty(empty));
    }

    [Fact]
    public void When_migration_description_is_null_or_empty_is_called_with_non_empty_string_it_should_return_false()
    {
        //Arrange
        var d = (MigrationDescription)"Add Source column";

        //Act + Assert
        Assert.False(MigrationDescription.IsNullOrEmpty(d));
    }
}
