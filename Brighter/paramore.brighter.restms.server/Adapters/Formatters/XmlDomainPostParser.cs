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
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.server.Adapters.Formatters
{
    internal class XmlDomainPostParser : IParseDomainPosts
    {
        readonly XmlSerializer feedDeserializer;
        readonly XmlSerializer pipeDeserializer;

        public XmlDomainPostParser ()
        {
            feedDeserializer = new XmlSerializer(typeof(RestMSFeed));
            pipeDeserializer = new XmlSerializer(typeof(RestMSPipeNew));
        }

        public Tuple<ParseResult, RestMSFeed, RestMSPipeNew> Parse(string body)
        {
            RestMSPipeNew pipeNew = null;
            RestMSFeed feedNew = null;
            if (TryParseNewPipe(body, out pipeNew))
            {
                return new Tuple<ParseResult, RestMSFeed, RestMSPipeNew>(ParseResult.NewPipe, feedNew, pipeNew);
            }
            else if (TryParseNewFeed(body, out feedNew))
            {
                return new Tuple<ParseResult, RestMSFeed, RestMSPipeNew>(ParseResult.NewFeed, feedNew, pipeNew);
            }

            return new Tuple<ParseResult, RestMSFeed, RestMSPipeNew>(ParseResult.Failed, feedNew, pipeNew);
        }

        bool TryParseNewFeed(string body, out RestMSFeed feedNew)
        {
            MemoryStream ms = null;
            try
            {
                ms = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(body));
                feedNew = (RestMSFeed)feedDeserializer.Deserialize(ms);
                return true;
            }
            catch (Exception e)
            {
                feedNew = null;
                return false;
            }
            finally
            {
                if (ms != null) 
                    ms.Dispose();
            }
        }

        bool TryParseNewPipe(string body, out RestMSPipeNew newPipe)
        {
            MemoryStream ms = null;
            try
            {
                ms = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(body));
                newPipe = (RestMSPipeNew) pipeDeserializer.Deserialize(ms);
                return true;
            }
            catch (Exception e)
            {
                newPipe = null;
                return false;
            }
            finally
            {
                if (ms != null) 
                    ms.Dispose();
            }
        }
            
    }
}