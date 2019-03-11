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
        /// The scope of the requests to store in the inbox - the default is everything
        /// </summary>
         public InboxScope Scope { get; }
        /// <summary>
        /// If null, the context to pass to the Inbox will be auto-generated from the handler class name. Otherwise
        /// override with a function that takes a type name - the target handler, and returns a string, the context key
        /// to use in the inbox to disambiguate requests (such as when an event is raised). 
        /// </summary>
        public Func<Type, string> Context { get; set; }

        public InboxConfiguration(
            InboxScope scope = InboxScope.All, 
            bool onceOnly = true,
            OnceOnlyAction actionOnExists = OnceOnlyAction.Throw, 
            Func<Type, string> context = null)
        {
            Scope = scope;
            Context = context;
            OnceOnly = onceOnly;
            ActionOnExists = actionOnExists;

            if (context == null)
            {
                Context = (handlerType) => handlerType.FullName;
            }
        }
    }
}
