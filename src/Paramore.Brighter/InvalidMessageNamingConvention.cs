#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter;

/// <summary>
/// Provides a naming convention for invalid message channels based on the data topic routing key.
/// Helps create consistent naming for invalid message channels across your application.
/// </summary>
/// <remarks>
/// The default template is "{0}.invalid" which appends ".invalid" to the data topic name.
/// For example, if the data topic is "orders", the invalid message channel will be "orders.invalid".
/// You can provide a custom template to override this behavior.
/// </remarks>
public class InvalidMessageNamingConvention
{
    private const string DEFAULT_TEMPLATE = "{0}.invalid";
    private readonly string _template;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidMessageNamingConvention"/> class.
    /// </summary>
    /// <param name="template">
    /// Optional string template for naming. Use {0} as placeholder for the data topic name.
    /// If null, defaults to "{0}.invalid".
    /// </param>
    public InvalidMessageNamingConvention(string? template = null)
    {
        _template = template ?? DEFAULT_TEMPLATE;
    }

    /// <summary>
    /// Creates a routing key for the invalid message channel based on the data topic routing key.
    /// </summary>
    /// <param name="dataTopic">The routing key of the data topic.</param>
    /// <returns>A <see cref="RoutingKey"/> for the invalid message channel.</returns>
    public RoutingKey MakeChannelName(RoutingKey dataTopic)
    {
        return new RoutingKey(string.Format(_template, dataTopic.Value));
    }
}
