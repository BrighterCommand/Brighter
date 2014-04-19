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
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class RequestHandlerAttribute : Attribute
    {
        private readonly int step;

        private readonly HandlerTiming timing;

        protected RequestHandlerAttribute(int step, HandlerTiming timing = HandlerTiming.Before)
        {
            this.step = step;
            this.timing = timing;
        }

        //We use this to pass params from the attribute into the instance of the handler
        //if you need to pass additional params to your handler, use this
        public virtual object[] InitializerParams()
        {
            return new object[0];
        }

        //In which order should we run this, within the pre or post sequence for the main target?
        public int Step
        {
            get { return step; }
        }

        //Should we run this before or after the main target?
        public HandlerTiming Timing
        {
            get { return timing; }
        }

        //What type do we implement for the Filter in the Command Processor Pipeline
        public abstract Type GetHandlerType();
    }
}