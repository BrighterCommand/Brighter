// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.restms
// Author           : ian
// Created          : 12-30-2014
//
// Last Modified By : ian
// Last Modified On : 12-31-2014
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
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace paramore.brighter.commandprocessor.messaginggateway.restms.Parsers
{
    /// <summary>
    /// Class XmlRequestBuilder.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public static class XmlRequestBuilder
    {
        /// <summary>
        /// Serialize the object to xml. Returns true if successful, false if failed. The out property is set to null on a failure, otherwise it contains the XML
        /// This code is designed to produce an XML Fragment suitable for use with a REST API. It omits the <xml/> declaration in the output
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="domainObject">The domain object.</param>
        /// <param name="body">The body.</param>
        /// <returns><c>true</c> if serialized, <c>false</c> otherwise.</returns>
        public static bool TryBuild<T>(T domainObject, out string body)
        {
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            var serializer = new XmlSerializer(typeof(T));
            var textWriter = new StringWriter();
            var writer = XmlWriter.Create(textWriter, new XmlWriterSettings { OmitXmlDeclaration = true });
            try
            {
                serializer.Serialize(writer, domainObject, ns);
                body = textWriter.ToString();
                return true;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                body = null;
                return false;
            }
            finally
            {
                writer.Dispose();
                textWriter.Dispose();
            }
        }
    }
}
