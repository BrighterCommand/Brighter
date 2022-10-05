using System;
using System.Diagnostics;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal class MyCommandToFail : ICommand
    { 
        public Guid Id { get; set; }
        
        /// <summary>
        /// Gets or sets the span that this operation live within
        /// </summary>
        public Activity Span { get; set; }
    }
}
