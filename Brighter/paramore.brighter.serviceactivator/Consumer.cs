// ***********************************************************************
// Assembly         : paramore.brighter.serviceactivator
// Author           : ian
// Created          : 07-01-2014
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

using System;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator
{
    /// <summary>
    /// Enum ConsumerState
    /// Identifies the state of a consumer: Open indicates that a consumer is reading messages from a channel, Shut means that a consumer is not reading messages
    /// from a channel
    /// </summary>
    public enum ConsumerState
    {
        /// <summary>
        /// The consumer is shut and won't read messages from the task queue
        /// </summary>
        Shut = 0,
        /// <summary>
        /// The consumer is open and will receive messages from the task queue
        /// </summary>
        Open = 1
    }

    /// <summary>
    /// Class Consumer.
    /// Manages the message pump used to read messages for a channel. Creation establishes the message pump for a given connection and channel. Open runs the
    /// message pump, which begins consuming messages from the channel; it returns the TPL Task used to run the message pump thread so that it can be
    /// Waited on by callers. Shut closes the message pump.
    /// 
    /// </summary>
    public class Consumer : IAmAConsumer, IEquatable<Consumer>
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public ConnectionName Name { get; set; }
        /// <summary>
        /// Gets the performer.
        /// </summary>
        /// <value>The performer.</value>
        public IAmAPerformer Performer { get; private set; }
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public ConsumerState State { get; set; }
        /// <summary>
        /// Gets or sets the job.
        /// </summary>
        /// <value>The job.</value>
        public Task Job { get; set; }

        public int JobId { get; set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="Consumer"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="channel">The channel.</param>
        /// <param name="messagePump">The message pump.</param>
        public Consumer(ConnectionName name, IAmAChannel channel, IAmAMessagePump messagePump)
        {
            Name = name;
            Performer = new Performer(channel, messagePump);
            State = ConsumerState.Shut;
        }

        /// <summary>
        /// Opens the task queue and begin receiving messages.
        /// </summary>
        public void Open()
        {
            State = ConsumerState.Open;
            Job = Performer.Run();
            JobId = Job.Id;
        }

        /// <summary>
        /// Shuts the task, which will not receive messages.
        /// </summary>
        public void Shut()
        {
            if (State == ConsumerState.Open)
            {
                Performer.Stop();
                State = ConsumerState.Shut;
            }
        }

        /// <summary>
        /// Shuts the consumer when the consumer is being released
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Performer.Dispose();
            }
        }

        /// <summary>
        /// Do the consumers match on name?
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        public bool Equals(Consumer other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Name, other.Name) && Equals(Job, other.Job);
        }

        /// <summary>
        ///Do the consumers match on name?
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Consumer)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Job != null ? Job.GetHashCode() : 0);
            }
        }

        /// <summary>
        /// Implements the ==. Do the consumers match on name?
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(Consumer left, Consumer right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the !=. Do the consumers not match on name?
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(Consumer left, Consumer right)
        {
            return !Equals(left, right);
        }
    }
}