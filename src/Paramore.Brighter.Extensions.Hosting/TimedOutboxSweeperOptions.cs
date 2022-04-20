﻿namespace Paramore.Brighter.Extensions.Hosting
{
    /// <summary>
    /// The configuration options for <see cref="TimedOutboxSweeper"/>
    /// </summary>
    public class TimedOutboxSweeperOptions
    {
        /// <summary>
        /// The timer interval in Seconds. 
        /// </summary>
        public int TimerInterval { get; set; } = 5;
        /// <summary>
        /// The age a message to pickup by the sweeper in milliseconds.
        /// </summary>
        public int MinimumMessageAge { get; set; } = 5000;

        /// <summary>
        /// The maximum number of messages to dispatch.
        /// </summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>
        /// Use bulk operations to dispatch messages.
        /// </summary>
        public bool UseBulk { get; set; } = false;
    }
}
