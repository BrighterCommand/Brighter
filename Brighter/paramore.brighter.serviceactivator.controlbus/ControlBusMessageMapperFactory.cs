// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator.controlbus
{
    public class ControlBusMessageMapperFactory : IAmAMessageMapperFactory
    {
        /// <summary>
        /// Creates the specified message mapper type.
        /// </summary>
        /// <param name="messageMapperType">Type of the message mapper.</param>
        /// <returns>IAmAMessageMapper.</returns>
        public IAmAMessageMapper Create(Type messageMapperType)
        {
            throw new NotImplementedException();
        }
    }
}