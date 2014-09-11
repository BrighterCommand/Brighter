// ***********************************************************************
// Assembly         : paramore.brighter.serviceactivator
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 09-11-2014
// ***********************************************************************
// <copyright file="Performer.cs" company="">
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

using System.Threading.Tasks;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator
{
    /// <summary>
    /// Class Performer.
    /// Abstracts the thread that runs a message pump
    /// </summary>
    public class Performer : IAmAPerformer 
    {
        private readonly IAmAnInputChannel channel;
        private readonly IAmAMessagePump messagePump;

        /// <summary>
        /// Initializes a new instance of the <see cref="Performer" /> class.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="messagePump">The message pump.</param>
        public Performer(IAmAnInputChannel channel, IAmAMessagePump messagePump)
        {
            this.channel = channel;
            this.messagePump = messagePump;
        }
        /// <summary>
        /// Stops this instance.
        /// </summary>
        public void Stop()
        {
            channel.Stop();
        }

        /// <summary>
        /// Runs this instance.
        /// </summary>
        /// <returns>Task.</returns>
        public Task Run()
        {
            return Task.Factory.StartNew(() => messagePump.Run(), TaskCreationOptions.LongRunning);
        }

    }
}