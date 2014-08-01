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

/// <summary>
/// The commandprocessor namespace.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
/// </summary>
namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Interface IAmASendMessageGateway{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
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