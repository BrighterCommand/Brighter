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
using System.Xml.Serialization;
using Paramore.Brighter.MessagingGateway.RESTMS.Logging;

namespace Paramore.Brighter.MessagingGateway.RESTMS.Parsers
{
    public class XmlResultParser
    {
        /// <summary>
        /// We don't want to create instances, but LibLog does not accept a static type.
        /// </summary>
        private XmlResultParser()
        { }

        public static bool TryParse<T>(string body, out T domainObject)
        {
            MemoryStream ms = null;
            var deserializer = new XmlSerializer(typeof(T));
            try
            {
                ms = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(body));
                domainObject = (T)deserializer.Deserialize(ms);
                return true;
            }
            catch (Exception e)
            {
                var logger = LogProvider.For<XmlResultParser>();
                logger.Trace(e.Message);
                domainObject = default(T);
                return false;
            }
            finally
            {
                ms?.Dispose();
            }
        }
    }
}
