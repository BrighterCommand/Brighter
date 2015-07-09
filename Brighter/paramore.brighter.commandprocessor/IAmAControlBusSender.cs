// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : SUNDANCE
// Created          : 07-09-2015
//
// Last Modified By : SUNDANCE
// Last Modified On : 07-09-2015
// ***********************************************************************
// <copyright file="IAmAControlBus.cs" company="">
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

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Interface IAmAControlBusSender
    /// This is really just a 'convenience' wrapper over a command processor to make it easy to configure two different command processors, one for normal messages the other for control messages.
    /// Why? The issue arises because an application providing a lot of monitoring messages might find that the load of those control messages begins to negatively impact the throughput of normal messages.
    /// To avoid this you can put control messages over a seperate broker. (There are some availability advantages here too).
    /// But many IoC containers make your life hard when you do this, as you have to indicate that you want to build the MonitorHandler with one command processor and the other handlers with another
    /// Wrapping the Command Processor in this class helps to alleviate that issue, by taking a dependency on a seperate interface.
    /// What goes over a control bus?
    /// 
    /// </summary>
    public interface IAmAControlBusSender
    {
        /// <summary>
        /// Posts the specified request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        void Post<T>(T request) where T : class, IRequest;
    }
}
