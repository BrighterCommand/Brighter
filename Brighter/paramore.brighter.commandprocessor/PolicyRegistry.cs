// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-02-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
// ***********************************************************************
// <copyright file="PolicyRegistry.cs" company="">
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

using System.Collections;
using System.Collections.Generic;
using Polly;

/// <summary>
/// The commandprocessor namespace.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
/// </summary>
namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class PolicyRegistry.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public class PolicyRegistry : IAmAPolicyRegistry, IEnumerable<KeyValuePair<string, Policy>>
    {
        readonly Dictionary<string, Policy> policies = new Dictionary<string, Policy>();

        /// <summary>
        /// Gets the specified policy name.
        /// </summary>
        /// <param name="policyName">Name of the policy.</param>
        /// <returns>Policy.</returns>
        public Policy Get(string policyName)
        {
            return policies.ContainsKey(policyName) ? policies[policyName] : null;
        }

        /// <summary>
        /// Adds the specified policy name.
        /// </summary>
        /// <param name="policyName">Name of the policy.</param>
        /// <param name="policy">The policy.</param>
        public void Add(string policyName, Policy policy)
        {
            policies.Add(policyName, policy);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<string, Policy>> GetEnumerator()
        {
            return policies.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}