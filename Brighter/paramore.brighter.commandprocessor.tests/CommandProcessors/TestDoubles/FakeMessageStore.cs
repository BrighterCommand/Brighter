#region Licence
/* The MIT License (MIT)
Copyright � 2015 Toby Henderson <hendersont@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    public class FakeMessageStore : IAmAMessageStore<Message>
    {
        public bool MessageWasAdded { get; set; }
        public Task Add(Message message)
        {
            var tcs = new TaskCompletionSource<object>();

            MessageWasAdded = true;

            tcs.SetResult(new object());
            return tcs.Task;
        }
        public Task<Message> Get(Guid messageId)
        {
            return null;
        }

        public Task<IList<Message>> Get(int pageSize = 100, int pageNumber = 1)
        {
            throw new NotImplementedException();
        }
    }
}