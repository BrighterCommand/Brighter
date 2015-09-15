// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 06-03-2015
//
// Last Modified By : ian
// Last Modified On : 06-03-2015
// ***********************************************************************
// <copyright file="InMemoryCommandStore.cs" company="">
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
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class InMemoryCommandStore.
    /// This is mainly intended to support developer tests where a persistent command store is not needed
    /// </summary>
    public class InMemoryCommandStore : IAmACommandStore
    {
        private readonly Dictionary<Guid, CommandStoreItem> _commands = new Dictionary<Guid, CommandStoreItem>();

        /// <summary>
        /// Adds the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="timeoutInMilliseconds"></param>
        public void Add<T>(T command, int timeoutInMilliseconds = -1) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<object>();

            if (!_commands.ContainsKey(command.Id))
            {
                _commands.Add(command.Id, new CommandStoreItem(typeof(T), string.Empty));
            }

            _commands[command.Id].CommandBody = JsonConvert.SerializeObject(command);
        }

        /// <summary>
        /// Finds the command with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="timeoutInMilliseconds"></param>
        /// <returns>ICommand.</returns>
        public T Get<T>(Guid id, int timeoutInMilliseconds = -1) where T:class, IRequest, new()
        {
            if (!_commands.ContainsKey(id))
                return null;

            var commandStoreItem = _commands[id];
            if (commandStoreItem.CommandType != typeof(T))
                throw new TypeLoadException(string.Format("The type of item {0) is {1} not{2}", id, commandStoreItem.CommandType.Name, typeof(T).Name));

            return JsonConvert.DeserializeObject<T>(commandStoreItem.CommandBody);
        }

                
        /// <summary>
        /// Class CommandStoreItem.
        /// </summary>
        class CommandStoreItem
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="T:System.Object"/> class.
            /// </summary>
            public CommandStoreItem(Type commandType, string commandBody, DateTime commandWhen)
            {
                CommandType = commandType;
                CommandBody = commandBody;
                CommandWhen = commandWhen;
            }

            public CommandStoreItem(Type commandType, string commandBody) : this(commandType, commandBody, DateTime.UtcNow){}

            /// <summary>
            /// Gets or sets the type of the command.
            /// </summary>
            /// <value>The type of the command.</value>
            public Type CommandType { get; set; }            
            /// <summary>
            /// Gets or sets the command body.
            /// </summary>
            /// <value>The command body.</value>
            public string CommandBody { get; set; }

            /// <summary>
            /// Gets or sets the command when.
            /// </summary>
            /// <value>The command when.</value>
            public DateTime CommandWhen { get; set; }
        }

    }
}
