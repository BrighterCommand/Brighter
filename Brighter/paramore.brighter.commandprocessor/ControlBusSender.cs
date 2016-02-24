// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 08-25-2015
//
// Last Modified By : ian
// Last Modified On : 08-25-2015
// ***********************************************************************
// <copyright file="ControlBusSender.cs" company="Ian Cooper">
//     Copyright \u00A9  2014 Ian Cooper
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
using System.Threading;
using System.Threading.Tasks;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class ControlBusSender.
    /// </summary>
    public class ControlBusSender : IAmAControlBusSender, IAmAControlBusSenderAsync, IDisposable
    {
        /// <summary>
        /// The command processor tat underlies the control bus; we only use the Post method
        /// </summary>
        private readonly CommandProcessor _commandProcessor;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlBusSender"/> class.
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        public ControlBusSender(CommandProcessor commandProcessor)
        {
            _commandProcessor = commandProcessor;
        }

        /// <summary>
        /// Posts the specified request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        public void Post<T>(T request) where T : class, IRequest
        {
            _commandProcessor.Post(request);
        }

        public async Task PostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken? ct = null) where T : class, IRequest
        {
            await _commandProcessor.PostAsync(request, continueOnCapturedContext, ct);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _commandProcessor.Dispose();

            _disposed = true;
        }

    }
}