// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class InvalidateCacheCommand : Command
    {
        public Uri ResourceToInvalidate { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        public InvalidateCacheCommand(Uri resourceToInvalidate) : base(Guid.NewGuid())
        {
            this.ResourceToInvalidate = resourceToInvalidate;
        }
    }
}
