// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-01-2014
// ***********************************************************************
// <copyright file="IAmACommandProcessor.cs" company="">
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

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Interface IAmACommandProcessor
    /// Paramore.Brighter.CommandProcessor provides the default implementation of this interface <see cref="CommandProcessor"/> and it is unlikely you need
    /// to override this for anything other than testing purposes. The usual need is that in a <see cref="RequestHandler{T}"/> you intend to publish an  
    /// <see cref="Event"/> to indicate the handler has completed to other components. In this case your tests should only verify that the correct 
    /// event was raised by listening to <see cref="Publish{T}"/> calls on this interface, using a mocking framework of your choice or bespoke
    /// Test Double.
    /// </summary>
    public interface IAmACommandProcessor
    {
        /// <summary>
        /// Sends the specified command.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        void Send<T>(T command) where T : class, IRequest;
        /// <summary>
        /// Publishes the specified event.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event">The event.</param>
        void Publish<T>(T @event) where T : class, IRequest;
        /// <summary>
        /// Posts the specified request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        void Post<T>(T request) where T : class, IRequest;
        /// <summary>
        /// Reposts the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        void Repost(Guid messageId);
    }
}