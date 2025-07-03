#region Licence

/* The MIT License (MIT)
Copyright © 2023 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter
{
    /// <summary>
    /// Defines how the message body content is encoded for transmission and storage.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="MessageBody"/> to determine how to interpret byte arrays
    /// when converting to and from string representations.
    /// </remarks>
    public enum CharacterEncoding
    {
        /// <summary>
        /// ASCII text encoding (7-bit character set).
        /// </summary>
        ASCII,

        /// <summary>
        /// Base64 encoding for binary data or ASCII armor for text.
        /// </summary>
        Base64,

        /// <summary>
        /// UTF-8 Unicode text encoding (variable-width character encoding).
        /// </summary>
        UTF8,

       /// <summary>
       /// UTF-16 Unicode text encoding (16-bit character encoding).
       /// </summary>
        UTF16,
        
        /// <summary>
        /// Raw binary data - conversion to string may result in data loss.
        /// </summary>
        /// <remarks>
        /// Use when the message body contains binary data that should not be 
        /// converted to text representation.
        /// </remarks>
        Raw,
    }
}
