// ***********************************************************************
// Assembly         : paramore.brighter.serviceactivator
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
// <copyright file="Connection.cs" company="">
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
using paramore.brighter.commandprocessor;

/// <summary>
/// The serviceactivator namespace.
/// </summary>
namespace paramore.brighter.serviceactivator
{
    /// <summary>
    /// Class Connection.
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public ConnectionName Name { get; set; }
        /// <summary>
        /// Gets the channel.
        /// </summary>
        /// <value>The channel.</value>
        public IAmAnInputChannel Channel { get; private set; }
        /// <summary>
        /// Gets the type of the data.
        /// </summary>
        /// <value>The type of the data.</value>
        public Type DataType { get; private set; }
        /// <summary>
        /// Gets the no of peformers.
        /// </summary>
        /// <value>The no of peformers.</value>
        public int NoOfPeformers { get; private set; }
        /// <summary>
        /// Gets the timeout in miliseconds.
        /// </summary>
        /// <value>The timeout in miliseconds.</value>
        public int TimeoutInMiliseconds { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Connection"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="channel">The channel.</param>
        /// <param name="dataType">Type of the data.</param>
        /// <param name="noOfPerformers">The no of performers.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        public Connection(ConnectionName name, IAmAnInputChannel channel, Type dataType, int noOfPerformers, int timeoutInMilliseconds)
        {
            Name = name;
            Channel = channel;
            DataType = dataType;
            NoOfPeformers = noOfPerformers;
            TimeoutInMiliseconds = timeoutInMilliseconds;
        }
    }
}