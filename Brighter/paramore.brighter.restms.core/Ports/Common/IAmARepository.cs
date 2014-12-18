// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 09-26-2014
//
// Last Modified By : ian
// Last Modified On : 10-10-2014
// ***********************************************************************
// <copyright file="IAmARepository.cs" company="">
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
using System.Collections.Generic;

namespace paramore.brighter.restms.core.Ports.Common
{
    /// <summary>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAmARepository<T> where T: class, IAmAnAggregate
    {
        /// <summary>
        /// Adds the specified aggregate.
        /// </summary>
        /// <param name="aggregate">The aggregate.</param>
        void Add(T aggregate);
        /// <summary>
        /// Gets the <see cref="T"/> at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>T.</returns>
        T this[Identity index] { get; }
        /// <summary>
        /// Removes the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        void Remove(Identity identity);

        /// <summary>
        /// Finds the specified query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>IEnumerable&lt;T&gt;.</returns>
         IEnumerable<T> Find(Func<T, bool> query);
    }
}
