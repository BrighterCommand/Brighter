#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter
{
    /// <summary>
    /// class WrapWithAttribute
    /// Indicates that you want to run a <see cref="IAmAMessageTransformAsync"/> after the <see cref="IAmAMessageMapper{TRequest}"/> has
    /// mapped the <see cref="IRequest"/> to a <see cref="Message"/>.
    /// Applied as an attribute to the <see cref="IAmAMessageMapper{TRequest}.MapToMessage"/> method
    /// </summary>
    public abstract class WrapWithAttribute : Attribute
    {           
        private int _step;

        protected WrapWithAttribute(int step)
        {
            _step = step;
        }
        
        //In which order should we run this
        /// <summary>
        /// Gets the step.
        /// </summary>
        /// <value>The step.</value>
        public int Step
        {
            get { return _step; }
            set { _step = value; }
        }    
        
        //What type do we implement for the Transform in the Message Mapper Pipeline
        /// <summary>
        /// Gets the type of the handler.
        /// </summary>
        /// <returns>Type.</returns>
        public abstract Type GetHandlerType();
    }
}
