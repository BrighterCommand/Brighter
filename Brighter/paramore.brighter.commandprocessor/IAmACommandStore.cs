﻿// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 06-02-2015
//
// Last Modified By : ian
// Last Modified On : 06-03-2015
// ***********************************************************************
// <copyright file="IAmACommandStore.cs" company="">
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

using System;
using System.Threading.Tasks;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Interface IAmACommandStore
    /// </summary>
    public interface IAmACommandStore
    {
        /// <summary>
        /// Adds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="timeoutInMilliseconds"></param>
        void Add<T>(T command, int timeoutInMilliseconds = -1) where T : class, IRequest;

        /// <summary>
        /// Finds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="timeoutInMilliseconds"></param>
        /// <returns>T.</returns>
        T Get<T>(Guid id, int timeoutInMilliseconds = -1) where T : class, IRequest, new();
    }
}
