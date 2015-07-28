// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ianp
// Created          : 25-03-2014
//
// Last Modified By : ian
// Last Modified On : 25-03-2014
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
using System.Collections.Generic;
using System.Linq;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeMessageStoreWithViewer : IAmAMessageStoreViewer<Message>, IAmAMessageStore<Message>
    {
        private readonly IList<Message> messages = new List<Message>();
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1)
        {
            return messages;
        }

        public void Add(Message message)
        {
            messages.Add(message);
        }

        public Message Get(Guid messageId)
        {
            return messages.SingleOrDefault(m => m.Id == messageId);
        }
    }
}