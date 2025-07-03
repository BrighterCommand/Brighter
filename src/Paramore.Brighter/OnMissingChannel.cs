#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
    /// Defines the action to take when required messaging infrastructure is missing.
    /// </summary>
    /// <remarks>
    /// Used by messaging gateways and channel factories to determine how to handle
    /// missing queues, topics, or other infrastructure components.
    /// </remarks>
    public enum OnMissingChannel
    {
        /// <summary>
        /// Create the required infrastructure via SDK calls if it doesn't exist.
        /// </summary>
        Create = 0,
        
        /// <summary>
        /// Validate that the infrastructure exists and raise an error if not found.
        /// </summary>
        Validate = 1,
        
        /// <summary>
        /// Assume the infrastructure exists without checking, fail fast if missing.
        /// </summary>
        /// <remarks>
        /// Use this option to remove the cost of existence checks on platforms 
        /// where such checks are expensive or when you're confident the 
        /// infrastructure is already in place.
        /// </remarks>
        Assume = 2
    }
}
