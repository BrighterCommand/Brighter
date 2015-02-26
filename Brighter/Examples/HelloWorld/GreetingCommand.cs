// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using paramore.brighter.commandprocessor;

namespace HelloWorld
{
    internal class GreetingCommand : IRequest
    {
        public GreetingCommand(string name)
        {
            Id = Guid.NewGuid();
            Name = name;
        }

        public Guid Id { get; set; }
        public string Name { get; private set; }
    }
}
