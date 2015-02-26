// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 11-05-2014
//
// Last Modified By : ian
// Last Modified On : 11-05-2014
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
using System.IO;
using System.Net.Mime;

namespace paramore.brighter.restms.core.Model
{
    /// <summary>
    /// Class MessageContent.
    /// </summary>
    public class MessageContent : IDisposable
    {
        private readonly Stream _content = new MemoryStream();
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="encoding">The encoding.</param>
        /// <param name="source">The source.</param>
        public MessageContent(ContentType contentType, TransferEncoding encoding, Stream source)
        {
            ContentType = contentType;
            Encoding = encoding;
            source.Position = 0;
            source.CopyTo(_content);
            _content.Position = 0;
        }

        /// <summary>
        /// Gets the type of the content.
        /// </summary>
        /// <value>The type of the content.</value>
        public ContentType ContentType { get; private set; }
        /// <summary>
        /// Gets the encoding.
        /// </summary>
        /// <value>The encoding.</value>
        public TransferEncoding Encoding { get; private set; }

        /// <summary>
        /// Ases the string.
        /// </summary>
        /// <returns>System.String.</returns>
        public string AsString()
        {
            _content.Position = 0;
            using (var sr = new StreamReader(_content))
            {
                return sr.ReadToEnd();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MessageContent()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _content.Dispose();
            }
        }

        /// <summary>
        /// Copies this instance.
        /// </summary>
        /// <returns>MessageContent.</returns>
        public MessageContent Copy()
        {
            return new MessageContent(ContentType, Encoding, _content);
        }
    }
}
