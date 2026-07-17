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

public class BoxTableNameIsNullOrEmptyTests
{
    [Fact]
    public void When_box_table_name_is_null_or_empty_is_called_with_null_it_should_return_true()
    {
        //Act + Assert
        Assert.True(BoxTableName.IsNullOrEmpty(null));
    }

    [Fact]
    public void When_box_table_name_is_null_or_empty_is_called_with_empty_string_it_should_return_true()
    {
        //Arrange
        var empty = (BoxTableName)"";

        //Act + Assert
        Assert.True(BoxTableName.IsNullOrEmpty(empty));
    }

    [Fact]
    public void When_box_table_name_is_null_or_empty_is_called_with_non_empty_string_it_should_return_false()
    {
        //Arrange
        var tableName = (BoxTableName)"Outbox";

        //Act + Assert
        Assert.False(BoxTableName.IsNullOrEmpty(tableName));
    }
}
