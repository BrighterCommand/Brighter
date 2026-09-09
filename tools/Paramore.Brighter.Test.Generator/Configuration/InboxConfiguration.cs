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
/// Represents the configuration for generating inbox tests.
/// </summary>
public class InboxConfiguration
{
    /// <summary>
    /// Gets or sets the prefix to use for the generated test class names.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the inbox provider implementation to test for the synchronous variant.
    /// If null or empty, the synchronous test suite is not generated.
    /// </summary>
    public string? InboxProvider { get; set; }

    /// <summary>
    /// Gets or sets the inbox provider implementation to test for the asynchronous variant.
    /// If null or empty, the asynchronous test suite is not generated.
    /// </summary>
    public string? InboxProviderAsync { get; set; }

    /// <summary>
    /// Gets or sets the namespace for the generated inbox test code. If null, uses the parent configuration's namespace.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the test category to apply to generated test classes.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the xUnit collection name to apply to generated test classes.
    /// </summary>
    public string? CollectionName { get; set; }
}
