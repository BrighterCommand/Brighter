// ***********************************************************************
// Assembly         : paramore.brighter.serviceactivator.controlbus
// Author           : ian
// Created          : 01-30-2015
//
// Last Modified By : ian
// Last Modified On : 01-30-2015
// ***********************************************************************
// <copyright file="ConfigurationMessageHandler.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator.controlbus.Ports.Commands;

namespace paramore.brighter.serviceactivator.controlbus.Ports.Handlers
{
    /// <summary>
    /// Class ConfigurationMessageHandler.
    /// </summary>
    public class ConfigurationMessageHandler : RequestHandler<ConfigurationMessage>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationMessageHandler" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public ConfigurationMessageHandler(ILog logger) : base(logger)
        {
        }
    }
}
