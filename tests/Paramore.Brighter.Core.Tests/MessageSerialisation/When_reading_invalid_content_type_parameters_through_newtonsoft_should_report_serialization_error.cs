#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia <irakli.gabisonia94@gmail.com>

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

#nullable enable

using System;
using Newtonsoft.Json;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class InvalidContentTypeParameterSerializationTests
{
    [Theory]
    [InlineData("""[{}]""")]
    [InlineData("""[{"Key":null,"Value":"orders"}]""")]
    [InlineData("""[{"Key":"profile","Value":null}]""")]
    public void When_reading_invalid_content_type_parameters_through_newtonsoft_should_report_serialization_error(string parameters)
    {
        // Arrange
        var json = "{\"ContentType\":{\"MediaType\":\"application/json\",\"Parameters\":" + parameters + "}}";

        // Act
        Exception exception = Record.Exception(() =>
        {
            MessageHeader? header = JsonConvert.DeserializeObject<MessageHeader>(json);
            _ = header!.ContentType.ToString();
        });

        // Assert
        Assert.IsType<JsonSerializationException>(exception);
    }
}
