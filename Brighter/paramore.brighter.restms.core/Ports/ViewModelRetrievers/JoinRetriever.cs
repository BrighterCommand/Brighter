// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 11-05-2014
//
// Last Modified By : ian
// Last Modified On : 11-05-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.core.Ports.ViewModelRetrievers
{
    /// <summary>
    /// Class JoinRetriever.
    /// </summary>
    public class JoinRetriever
    {
        private readonly IAmARepository<Join> _joinRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="JoinRetriever"/> class.
        /// </summary>
        /// <param name="joinRepository">The join repository.</param>
        /// <param name="logger">The logger.</param>
        public JoinRetriever(IAmARepository<Join> joinRepository)
        {
            _joinRepository = joinRepository;
        }

        /// <summary>
        /// Retrieves the specified join name.
        /// </summary>
        /// <param name="joinName">Name of the join.</param>
        /// <returns>RestMSJoin.</returns>
        /// <exception cref="paramore.brighter.restms.core.Ports.Common.JoinDoesNotExistException"></exception>
        public RestMSJoin Retrieve(Name joinName)
        {
            var join = _joinRepository[new Identity(joinName.Value)];

            if (join == null)
            {
                throw new JoinDoesNotExistException(string.Format("There is no join with the name {0}", joinName));
            }

            return new RestMSJoin(join);
        }
    }
}
