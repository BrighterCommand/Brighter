#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.Inbox;

namespace Paramore.Brighter
{
    [Flags]
    public enum InboxScope
    {
        Commands = 1,
        Events = 2,
        All = 3
    }
    
    
    /// <summary>
    /// Configure the inbox globally
    /// </summary>
    public class InboxConfiguration
    {
       /// <summary>
        /// What should we do if exists, defaults to OnceOnlyAction.Throw - let the caller handle
        /// </summary>
        public OnceOnlyAction ActionOnExists { get; }
       
        /// <summary>
        /// Use the inbox to de-duplicate requests - defaults to true
        /// </summary>
        public bool OnceOnly { get; }
        
        /// <summary>
        /// The Inbox to use - defaults to InMemoryInbox
        /// </summary>
        public IAmAnInbox Inbox { get;}
        
        /// <summary>
        /// The scope of the requests to store in the inbox - the default is everything
        /// </summary>
         public InboxScope Scope { get; }
        
        /// <summary>
        /// If null, the context to pass to the Inbox will be auto-generated from the handler class name. Otherwise
        /// override with a function that takes a type name - the target handler, and returns a string, the context key
        /// to use in the inbox to disambiguate requests (such as when an event is raised). 
        /// </summary>
        public Func<Type, string>? Context { get; set; }

        public InboxConfiguration(
            IAmAnInbox? inbox = null,
            InboxScope scope = InboxScope.All, 
            bool onceOnly = true,
            OnceOnlyAction actionOnExists = OnceOnlyAction.Throw, 
            Func<Type, string>? context = null)
        {
            Inbox = inbox ?? new InMemoryInbox(TimeProvider.System); 
            
            Scope = scope;
            Context = context;
            OnceOnly = onceOnly;
            ActionOnExists = actionOnExists;

            if (context == null)
            {
                Context = (handlerType) => handlerType?.FullName ?? string.Empty;
            }
        }
    }
}
