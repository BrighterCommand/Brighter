// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-15-2014
//
// Last Modified By : ian
// Last Modified On : 07-16-2014
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

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Interface IAmAMessageMapperFactory
    /// In order to use a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> approach we require you to provide
    /// a <see cref="IAmAMessageMapper"/> to map between <see cref="Command"/> or <see cref="Event"/> and a <see cref="Message"/> registered via <see cref="IAmAMessageMapperRegistry"/>
    /// We then call the instance of the factory which the client provides to create instances of that <see cref="IAmAMessageMapper"/>. You will need to implement the
    /// <see cref="IAmAMessageMapperFactory"/> to use the Task Queue approach, and provide the instance of your mapper on request. Typically you might use an IoC container
    /// to implement this.
    /// </summary>
    public interface IAmAMessageMapperFactory
    {
        /// <summary>
        /// Creates the specified message mapper type.
        /// </summary>
        /// <param name="messageMapperType">Type of the message mapper.</param>
        /// <returns>IAmAMessageMapper.</returns>
        IAmAMessageMapper Create(Type messageMapperType);
    }
}