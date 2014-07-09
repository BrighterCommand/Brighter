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
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace paramore.brighter.commandprocessor
{
    class MessageMapperRegistry : IAmAMessageMapperRegistry, IEnumerable<KeyValuePair<Type, object>>
    {
        readonly Dictionary<Type, object> messageMappers = new Dictionary<Type, object>(); 
        public IAmAMessageMapper<TRequest> Get<TRequest>() where TRequest : class, IRequest
        {
            if (messageMappers.ContainsKey(typeof (TRequest)))
                return (IAmAMessageMapper<TRequest>) messageMappers[typeof (TRequest)];
            else
                return (IAmAMessageMapper<TRequest>) null;

        }

        //support object initializer
        public void Add(Type messageType, object messageMapper)
        {
            messageMappers.Add(messageType, messageMapper);
        }

        public void Register<TRequest, TMessageMapper>(TMessageMapper mapper) where TRequest: class, IRequest where TMessageMapper : class, IAmAMessageMapper<TRequest>
        {
            if (messageMappers.ContainsKey(typeof(TRequest)))
                throw new ArgumentException(string.Format("Message type {0} alread has a mapper; only one mapper can be registred per type", typeof(TRequest).Name));

            messageMappers.Add(typeof(TRequest), mapper);
        }

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator()
        {
            return messageMappers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
