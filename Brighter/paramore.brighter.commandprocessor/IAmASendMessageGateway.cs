// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-29-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
// <copyright file="IAmASendMessageGateway.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
using System;
using System.Threading.Tasks;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Interface IAmASendMessageGateway
    /// Abstracts away the Application Layer used to push messages onto a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a>
    /// Usually clients do not need to instantiate as access is via an <see cref="IAmAChannel"/> derived class.
    /// We provide the following default gateway applications
    /// <list type="bullet">
    /// <item>AMQP</item>
    /// <item>RESTML</item>
    /// </list>
    /// </summary>
    public interface IAmASendMessageGateway: IDisposable
    {
        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        Task Send(Message message);
    }
}