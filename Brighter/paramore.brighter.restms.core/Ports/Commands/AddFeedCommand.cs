// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-01-2014
//
// Last Modified By : ian
// Last Modified On : 10-07-2014
// ***********************************************************************
// <copyright file="NewFeedCommand.cs" company="">
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
using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    /// <summary>
    /// Class NewFeedCommand.
    /// </summary>
    public class AddFeedCommand : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Command" /> class.
        /// </summary>
        /// <param name="domainName">Name of the domain.</param>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="title">The title.</param>
        /// <param name="license">The license.</param>
        public AddFeedCommand(string domainName, string name, string type = null, string title = null, string license = null) : base(Guid.NewGuid())
        {
            DomainName = domainName;
            Name = name;
            Title = title;
            License = license;
            Type = type;
        }

        /// <summary>
        /// Gets or sets the name of the domain.
        /// </summary>
        /// <value>The name of the domain.</value>
        public string DomainName { get; set; }
        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; private set; }
        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <value>The type.</value>
        public string Type { get; private set; }
        /// <summary>
        /// Gets the title.
        /// </summary>
        /// <value>The title.</value>
        public string Title { get; private set; }
        /// <summary>
        /// Gets the license.
        /// </summary>
        /// <value>The license.</value>
        public string License { get; private set; }
    }
}
