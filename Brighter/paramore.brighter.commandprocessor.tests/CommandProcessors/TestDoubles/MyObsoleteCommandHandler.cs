// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyObsoleteCommandHandler : RequestHandler<MyCommand>
    {
        private static MyCommand s_command;

        public MyObsoleteCommandHandler(ILog logger)
            : base(logger)
        {
            s_command = null;
        }

        [MyPreValidationHandlerAttribute(step: 2, timing: HandlerTiming.Before)]
        [MyPostLoggingHandlerAttribute(step: 1, timing: HandlerTiming.After)]
        [Obsolete] // even with attributes non inherting from MessageHandlerDecoratorAttribute it should not fail
        public override MyCommand Handle(MyCommand command)
        {
            LogCommand(command);
            return base.Handle(command);
        }

        public static bool Shouldreceive(MyCommand expectedCommand)
        {
            return (s_command != null) && (expectedCommand.Id == s_command.Id);
        }

        private void LogCommand(MyCommand request)
        {
            s_command = request;
        }
    }
}
