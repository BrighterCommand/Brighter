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

namespace paramore.commandprocessor.tests.RestMSServer
{
    public class NewsFeedPublishSubscribeTests
    {
        #region Scenario
        /*
         * http://zyre-v1.wikidot.com/doc:restms-quick-reference
         * Newsfeed publish-subscribe scenario

        * In which publishers distribute messages to subscribers:

        * AMQP publisher: 
         * declare topic exchange: 
         *  Exchange.Declare name="{feed-name}" type="topic", 
         * then publish messages to that exchange: 
         *  Basic.Publish exchange="{feed-name}" routing-key="{category}"

        * AMQP subscriber: 
         * use private queue: 
         *  Queue.Declare queue="(empty)" exclusive=1, 
         * then 
         *  Basic.Consume 
         * on queue. 
         * To subscribe, bind queue to feed exchange, using category pattern: 
         * Queue.Bind queue="{queue}" exchange="{feed-name}" routing-key="{category pattern}"

        * RestMS publisher: 
         * create public feed: 
         *  POST <feed name="{feed-name}" type="topic"/> to /restms/domain/, 
         * then publish messages to that feed: 
         *  POST <message address="{category}"/> to /restms/feed/{feed-name}.

        * RestMS subscriber: 
         * create pipe: 
         *  POST <pipe/> to /restms/domain/. 
         * Create join from pipe to feed: 
         *  POST <join address="{category pattern}" feed="{feed-name}"/> 
         * Then retrieve message asynclet: GET /restms/resource/{asynclet-hash}.

         */
        #endregion
    }
}
