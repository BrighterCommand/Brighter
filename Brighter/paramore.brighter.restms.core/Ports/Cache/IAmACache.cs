// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace paramore.brighter.restms.core.Ports.Cache
{
    public interface IAmACache
    {
        void InvalidateResource(Uri resourceToInvalidate);
    }
}
