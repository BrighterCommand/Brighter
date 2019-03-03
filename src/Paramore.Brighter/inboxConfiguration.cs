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
        /// If true, the context to pass to the Inbox will be auto-generated from the handler class name. If false
        /// we will not use a context, which is not recommended, unless you only use a scope of Commands. 
        /// </summary>
        public bool UseAutoContext { get; }

        public InboxConfiguration(InboxScope scope = InboxScope.All, bool useAutoContext = true, bool onceOnly = true, OnceOnlyAction actionOnExists = OnceOnlyAction.Throw)
        {
            Scope = scope;
            UseAutoContext = useAutoContext;
            OnceOnly = onceOnly;
            ActionOnExists = actionOnExists;
        }
    }
}
