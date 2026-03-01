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

using System.Collections.Generic;

namespace Paramore.Brighter.Test.Generator.Configuration;

/// <summary>
/// Represents the configuration for generating test code.
/// </summary>
public class TestConfiguration
{
    /// <summary>
    /// Gets or sets the namespace for the generated test code.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the destination folder where the generated test files will be written.
    /// </summary>
    public string DestinationFolder { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the message factory to use for creating test messages.
    /// </summary>
    public string? MessageFactory { get; set; }
    
    /// <summary>
    /// Gets or sets the outbox configuration for generating outbox tests.
    /// </summary>
    public OutboxConfiguration? Outbox { get; set; }
    
    /// <summary>
    /// Gets or sets a dictionary of named outbox configurations for generating multiple outbox tests.
    /// </summary>
    public Dictionary<string, OutboxConfiguration>? Outboxes { get; set; }
}
