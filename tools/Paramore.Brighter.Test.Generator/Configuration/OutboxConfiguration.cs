#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Test.Generator.Configuration;

/// <summary>
/// Represents the configuration for generating outbox tests.
/// </summary>
public class OutboxConfiguration
{
    /// <summary>
    /// Gets or sets the prefix to use for the generated test class names.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the transaction type used by the outbox implementation.
    /// </summary>
    public string Transaction { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the outbox provider implementation to test.
    /// </summary>
    public string OutboxProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the namespace for the generated outbox test code. If null, uses the parent configuration's namespace.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the message factory for creating test messages. If null, uses the parent configuration's message factory.
    /// </summary>
    public string? MessageFactory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the outbox provider supports transactions.
    /// </summary>
    public bool SupportsTransactions { get; set; } = true;

    public string? Category { get; set; }
}
