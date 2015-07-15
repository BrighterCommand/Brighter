// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-02-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
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

using System;
using System.Collections;
using System.Collections.Generic;
using Polly;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class PolicyRegistry
    /// We use <a href="">Polly</a> policies to provide Quality of Service. 
    /// By default we provide them for a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> and require you to register policies with
    /// the policy names of:
    /// <list type="bullet">
    /// <item>CommandProcessor.RETRYPOLICY</item>
    /// <item>CommandProcessor.CIRCUTIBREAKER</item>
    /// </list>
    /// to respectively determine retry attempts for putting onto and popping off the queue and for breaking the circuit if we cannot
    /// You can register additional policies (or reuse these) to provide QoS for individual handlers. The <see cref="UsePolicyAttribute"/> and <see cref="ExceptionPolicyandler"/>
    /// provide an easy way to do this using the policies that you add to this registry
    /// This is a default implementation of <see cref="IAmAPolicyRegistry"/> and is suitable for most purposes excluding testing
    /// </summary>
    public class PolicyRegistry : IAmAPolicyRegistry, IEnumerable<KeyValuePair<string, Policy>>
    {
        private readonly Dictionary<string, Policy> _policies = new Dictionary<string, Policy>();

        /// <summary>
        /// Gets the specified policy name.
        /// </summary>
        /// <param name="policyName">Name of the policy.</param>
        /// <returns>Policy.</returns>
        /// <exception cref="ArgumentException">There is no policy for policyName</exception>
        public Policy Get(string policyName)
        {
            if (_policies.ContainsKey(policyName))
            {
                return _policies[policyName];
            }

            throw new ArgumentException(String.Format("There is no policy for {0}", policyName), policyName);
        }

        public bool Has(string policyName)
        {
            return _policies.ContainsKey(policyName);
        }

        /// <summary>
        /// Adds the specified policy name.
        /// </summary>
        /// <param name="policyName">Name of the policy.</param>
        /// <param name="policy">The policy.</param>
        public void Add(string policyName, Policy policy)
        {
            _policies.Add(policyName, policy);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<string, Policy>> GetEnumerator()
        {
            return _policies.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}