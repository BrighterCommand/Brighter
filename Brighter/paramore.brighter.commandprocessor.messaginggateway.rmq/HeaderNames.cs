// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 01-26-2015
//
// Last Modified By : ian
// Last Modified On : 02-26-2015
// ***********************************************************************
// <copyright file="HeaderNames.cs" company="">
//     Copyright (c) . All rights reserved.
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

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class HeaderNames.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public class HeaderNames
    {
        /// <summary>
        /// The messag e_ type{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
        /// </summary>
        public const string MESSAGE_TYPE = "MessageType";
        /// <summary>
        /// The messag e_ identifier{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
        /// </summary>
        public const string MESSAGE_ID = "MessageId";
        /// <summary>
        /// The correlation id
        /// </summary>
        public const string CORRELATION_ID = "CorrelationId";
        /// <summary>
        /// The topic{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
        /// </summary>
        public const string TOPIC = "Topic";
        /// <summary>
        /// The handle d_ count{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
        /// </summary>
        public const string HANDLED_COUNT = "HandledCount";
        /// <summary>
        /// The milliseconds to delay the message by (requires plugin rabbitmq_delayed_message_exchange)
        /// </summary>
        public const string DELAY_MILLISECONDS = "x-delay";
        /// <summary>
        /// The milliseconds the message was instructed to be delayed for (sent as negative) (requires plugin rabbitmq_delayed_message_exchange)
        /// </summary>
        public const string DELAYED_MILLISECONDS = "x-delay";
        /// <summary>
        /// Indicates the original id of this message given a historic scenario (e.g. re-queueuing).
        /// </summary>
        public const string ORIGINAL_MESSAGE_ID = Message.OriginalMessageIdHeaderName;
        /// <summary>
        /// Tag used to identify this message in the sequence against its Id (used to perform multiple ack against Id upto Tag).
        /// </summary>
        public const string DELIVERY_TAG = Message.DeliveryTagHeaderName;

        /// <summary>
        /// The payload content type as written to the message bus
        /// </summary>
        public const string CONTENT_TYPE = "ContentType";

        ///// <summary>
        ///// Conveys the RMQ BasicDeliverEventArgs.Type field, which
        ///// when used holds an indicator of what type of object/class
        ///// the serialized payload holds.
        ///// </summary>
        //public const string CONTENT_SERIALIZED_TYPE = "ContentSerializedType";
    }
}