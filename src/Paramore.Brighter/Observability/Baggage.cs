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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Represents the W3C Baggage header, which conveys user-defined request or workflow information across systems.
/// Implementation follows the W3C specification: https://www.w3.org/TR/baggage/
/// </summary>
/// <remarks>
/// Each entry consists of a key-value pair where the key identifies the vendor and the value contains user-defined request or workflow data.
/// Note that baggage entries are not intended for telemetry data, but rather for user-defined metadata about a trace.
/// </remarks>
public class Baggage : IEnumerable<KeyValuePair<string, string?>>
{
    private readonly Dictionary<string, string> _entries = new();
    private const int MaxKeyValuePairs = 32;
    private static readonly Regex KeyRegex = new("^[a-zA-Z0-9][a-zA-Z0-9_\\-*/]{0,255}$", RegexOptions.Compiled);
    private static readonly Regex ValueRegex = new("^[\\x20-\\x2b\\x2d-\\x3c\\x3e-\\x7e]{0,255}[\\x21-\\x2b\\x2d-\\x3c\\x3e-\\x7e]$", RegexOptions.Compiled);
    public static Baggage Empty { get; } = new Baggage();

    /// <summary>
    /// Adds a vendor-specific key-value pair to the TraceState.
    /// </summary>
    /// <param name="key">The vendor identifier. Must be a valid lowercase alphanumeric string with allowed special characters.</param>
    /// <param name="value">The vendor-specific trace value. Must contain only valid ASCII characters.</param>
    /// <exception cref="ArgumentException">Thrown when key or value is null, empty, or doesn't match the required format.</exception>
    /// <exception cref="InvalidOperationException">Thrown when attempting to add more than the maximum allowed entries.</exception>
    /// <remarks>
    /// Keys must be lowercase alphanumeric strings and may contain '_', '-', '*', '/' characters.
    /// Values must contain only ASCII characters specified in the W3C trace-context specification.
    /// </remarks>
    public void Add(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty", nameof(value));
            
        if (!KeyRegex.IsMatch(key))
            throw new ArgumentException("Invalid key format", nameof(key));
            
        if (!ValueRegex.IsMatch(value))
            throw new ArgumentException("Invalid value format", nameof(value));
            
        if (_entries.Count >= MaxKeyValuePairs)
            throw new InvalidOperationException($"TraceState cannot contain more than {MaxKeyValuePairs} entries");

        _entries[key] = value;
    }
    
    /// <summary>
    /// Returns an enumerator that iterates through the trace state entries.
    /// </summary>
    /// <returns>An enumerator for the trace state entries.</returns>
    public IEnumerator<KeyValuePair<string, string?>> GetEnumerator()
    {
        return _entries.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the trace state entries.
    /// </summary>
    /// <returns>An enumerator for the trace state entries.</returns>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Adds baggage entries in the W3C baggage format.
    /// </summary>
    /// <param name="baggage">A string containing comma-separated key-value pairs in the format "key1=value1,key2=value2"</param>
    /// <exception cref="ArgumentException">Thrown when the baggage string is invalid or contains invalid key-value pairs.</exception>
    /// <exception cref="InvalidOperationException">Thrown when adding entries would exceed the maximum allowed pairs.</exception>
    public void LoadBaggage(string? baggage)
    {
        if (string.IsNullOrEmpty(baggage))
            return;

        var pairs = baggage!.Split([','], StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var keyValue = pair.Split(['='], 2);
            if (keyValue.Length != 2)
                throw new ArgumentException($"Invalid tracestate format: {pair}", nameof(baggage));

            Add(keyValue[0], keyValue[1]);
        }
    }

    /// <summary>
    /// Returns a string representation of the TraceState in the W3C trace-context format.
    /// </summary>
    /// <returns>A comma-separated list of key-value pairs in the format "key1=value1,key2=value2".</returns>
    public override string ToString()
    {
        return string.Join(",", _entries.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }

    /// <summary>
    /// Baggage can be created from a string representation of the W3C Baggage header.
    /// </summary>
    /// <param name="baggageString">The comma seperated string representing the W3C baggage</param>
    /// <returns></returns>
    public static Baggage FromString(string baggageString)
    {
       var baggage = new Baggage();
        baggage.LoadBaggage(baggageString);
        return baggage;
    }
}

