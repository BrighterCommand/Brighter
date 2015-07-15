﻿// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-29-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Interface IAmAChannelFactory
    /// Creates instances of <see cref="IAmAChannel"/>channels. We provide support for some Application Layer channels, and provide factories for those:
    /// <list type="bullet">
    /// <item>AMQP</item>
    /// <item>RestML</item>
    /// </list>
    /// If you need to support other Application Layer protocols, please consider issuing a Pull request for your implementation
    /// </summary>
    public interface IAmAChannelFactory
    {
        /// <summary>
        /// Creates the input channel.
        /// </summary>
        /// <param name="channelName">Name of the channel.</param>
        /// <param name="routingKey"></param>
        /// <param name="isDurable"></param>
        /// <returns>IAmAnInputChannel.</returns>
        IAmAnInputChannel CreateInputChannel(string channelName, string routingKey, bool isDurable);

        /// <summary>
        /// Creates the output channel.
        /// </summary>
        /// <param name="routingKey"></param>
        /// <returns>IAmAnOutputChannel.</returns>
        IAmAnOutputChannel CreateOutputChannel(string routingKey);
    }
}
