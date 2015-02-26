// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using System.Collections.Generic;
using System.Linq;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    public class FakeSession : IUnitOfWork
    {
        private readonly IList<object> _identityMap = new List<object>();

        public void Add<T>(T aggregate)
        {
            _identityMap.Add(aggregate);
        }

        public void Commit() { }

        public T Load<T>(int id) where T : IAmAnAggregate
        {
            return _identityMap.Cast<T>().Where(aggregate => aggregate.ID == id).Select(aggregate => aggregate).FirstOrDefault();
        }

        public IEnumerable<T> Query<T>() where T : IAmAnAggregate
        {
            return _identityMap.Cast<T>();
        }
    }
}
