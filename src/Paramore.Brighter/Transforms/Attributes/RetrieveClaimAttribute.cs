#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Transforms.Attributes;

/// <summary>
/// Configures middleware to retrieve a payload from a message store and writes it to the body of the message
/// </summary>
public class RetrieveClaimAttribute : UnwrapWithAttribute
{
    private readonly bool _retain;

    /// <summary>
    /// Retrieves 'luggage' matching a claim id from storage
    /// </summary>
    /// <param name="step">The order in which transformations are applied</param>
    /// <param name="retain">Should we retain the 'luggage' in the store once deleted</param>
    public RetrieveClaimAttribute(int step, bool retain = false) : base(step)
    {
        _retain = retain;
    }

    public override object[] InitializerParams()
    {
        return [_retain];
    }

    /// <summary>
    /// Gets the retrieve luggage transformer type
    /// </summary>
    /// <returns>The type of the Claims Check Transformer</returns>
    public override Type GetHandlerType()
    {
        return typeof(ClaimCheckTransformer);
    }
}
