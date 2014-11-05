// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-21-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
// <copyright file="Pipe.cs" company="">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using paramore.brighter.restms.core.Extensions;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Model
{
    /// <summary>
    ///    Pipe - a source of messages delivered to applications.
    ///    Pipes follow these rules:
    ///    A pipe is a read-only ordered stream of messages meant for a single reader.
    ///    The order of messages in a pipe is stable only for a single feed.
    ///    Pipes receive messages from joins, according to the joins defined between the pipe and the feed.
    ///    Clients MUST create pipes for their own use: all pipes are private and dynamic.
    ///    To create a new pipe the client POSTs a pipe document to the parent domain's URI.
    ///    The server MAY do garbage collection on unused, or overflowing pipes.
    ///    http://www.restms.org/spec:2
    /// </summary>
    public class Pipe : Resource, IAmAnAggregate
    {
        SortedList<long, List<Message>> messages = new SortedList<long, List<Message>>();
        readonly object messageSyncRoot = new object();
        readonly ConcurrentBag<Join> joins = new ConcurrentBag<Join>(); 
        const string PIPE_URI_FORMAT = "http://{0}/restms/pipe/{1}";

        /// <summary>
        /// Gets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        public Identity Id { get; private set; }
        /// <summary>
        /// Gets the version.
        /// </summary>
        /// <value>The version.</value>
        public AggregateVersion Version { get; private set; }
        /// <summary>
        /// Gets the title.
        /// </summary>
        /// <value>The title.</value>
        public Title Title { get; private set; }
        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <value>The type.</value>
        public PipeType Type { get; private set; }

        /// <summary>
        /// Gets the <see cref="Join"/> that connects the <see cref="Pipe"/> to a <see cref="Feed"/>.
        /// </summary>
        /// <value>The join.</value>
        public IEnumerable<Join> Joins { get { return joins; }
        }

        /// <summary>
        /// Gets the messages on the <see cref="Pipe"/> distributed from the <see cref="Feed"/> because they matched the <see cref="Join"/>.
        /// Messages are shown in order of time of being added to the pipe
        /// </summary>
        /// <value>The messages.</value>
        public IEnumerable<Message> Messages { get { return messages.Values.SelectMany(messageList => messageList); } }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pipe"/> class.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="pipeType">Type of the pipe.</param>
        /// <param name="title">The title.</param>
        public Pipe(string identity, string pipeType, string title= null)
        {
            Id = new Identity(identity);
            Title = new Title(title);
            Name = new Name(Id.Value);
            Type = (PipeType) Enum.Parse(typeof (PipeType), pipeType);
            Href = new Uri(string.Format(PIPE_URI_FORMAT, Globals.HostName, Id.Value));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pipe"/> class.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <param name="pipeType">Type of the pipe.</param>
        /// <param name="title">The title.</param>
        public Pipe(Identity identity, PipeType pipeType, Title title = null)
        {
            Id = identity;
            Title = title;
            Name = new Name(Id.Value);
            Type = pipeType;
            Href = new Uri(string.Format(PIPE_URI_FORMAT, Globals.HostName, identity.Value));
        }

        public void AddJoin(Join join)
        {
            joins.Add(join);
        }

        /// <summary>
        /// Adds the message. Messages are ordered by time of arrival
        /// </summary>
        /// <param name="message">The message.</param>
        public void AddMessage(Message message)
        {
            lock (messageSyncRoot)
            {
                long ticks = DateTime.UtcNow.Ticks;
                var matchingKey = messages.Keys.FirstOrDefault(key => key == ticks);
                if (matchingKey == 0)
                {
                    messages.Add(ticks, new List<Message>() {message});    
                }
                else
                {
                    messages[ticks].Add(message);
                }

                message.PipeName = Name;
            }
        }

        /// <summary>
        /// Deletes the message. When we delete a message, we delete all older messages.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        public void DeleteMessage(Guid messageId)
        {
            lock (messageSyncRoot)
            {
                var matchingKey = FindNodeContainingMessage(messageId);
                if (matchingKey == 0) return; 

                var partitionKey = DeleteRequestedMessage(matchingKey, messageId);

                DeleteOlderMessages(partitionKey);
            }
        }

        long FindNodeContainingMessage(Guid messageId)
        {
            var matchingKey = (
                from key in messages.Keys
                let messageList = messages[key]
                let message = messageList.FirstOrDefault(msg => msg.MessageId == messageId)
                where message != null
                select key
                )
                .FirstOrDefault();
            return matchingKey;
        }

        int DeleteRequestedMessage(long matchingKey, Guid messageId)
        {
            int partitionKey = messages.IndexOfKey(matchingKey);
            var messageList = messages[matchingKey];
            var message = messageList.FirstOrDefault(msg => msg.MessageId == messageId);
            
            if (message == null) return partitionKey;

            message.Content.Dispose();
            messageList.Remove(message);
            if (!messageList.Any())
            {
                messages.Remove(matchingKey);
                partitionKey = partitionKey <= 1 ? 0 : partitionKey--;
            }
            return partitionKey;
        }

        void DeleteOlderMessages(int partitionKey)
        {
            var partitionedLists = PartitionOlderFromNewerThanMessage(partitionKey);
            messages = partitionedLists.Item2;
            DeleteOldMessages(partitionedLists.Item1);
        }

        Tuple<SortedList<long, List<Message>>, SortedList<long, List<Message>>> PartitionOlderFromNewerThanMessage(int partitionKey)
        {
            var olderMessageList = new SortedList<long, List<Message>>();

            for (var j = 0; j <= partitionKey; j++)
            {
                var key = messages.Keys[j];
                olderMessageList.Add(key, messages[key]);
            }

            var newerMessageList = new SortedList<long, List<Message>>();
            var messageListLength = messages.Count();
            for (var i = partitionKey + 1; i < messageListLength; i++)
            {
                var key = messages.Keys[i];
                newerMessageList.Add(key, messages[key]);
            }

            return new Tuple<SortedList<long, List<Message>>, SortedList<long, List<Message>>>(olderMessageList, newerMessageList);
        }

        void DeleteOldMessages(SortedList<long, List<Message>> olderMessages)
        {
            olderMessages.Values.SelectMany(messageList => messageList).Each(message => message.Content.Dispose());
        }

        public Message FindMessage(Guid messageId)
        {
            return (from key in messages.Keys
             let messageList = messages[key]
             let message = messageList.FirstOrDefault(msg => msg.MessageId == messageId)
             select message).FirstOrDefault();
        }
    }
}
