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
/// Configures large payloads that may not be possible on certain middleware platforms, or perform badly on others. A claim check allows us to
/// offload the message body to distributed storage, from where it can be retrieved later.
/// </summary>
public class ClaimCheckAttribute : WrapWithAttribute
{
    private readonly int _thresholdInKb;

    /// <summary>
    /// Checks large payloads, 'luggage', into storage
    /// </summary>
    /// <param name="step">The order to run this in</param>
    /// <param name="thresholdInKb">The size in Kb over which luggage should be checked, 0 for all. So 256 represents 256Kb</param>
    public ClaimCheckAttribute(int step, int thresholdInKb = 0) : base(step)
    {
        _thresholdInKb = thresholdInKb;
    }

    public override object[] InitializerParams()
    {
        return [_thresholdInKb];
    }

    /// <summary>
    /// Get the Claims Check Transformer type 
    /// </summary>
    /// <returns>The type of the Claims Check Transformer</returns>
    public override Type GetHandlerType()
    {
        return typeof(ClaimCheckTransformer);
    }
}
