﻿#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Transforms.Storage
{
    /// <summary>
    /// Provides storage for a message body that is too large to transmit over message-oriented middleware
    /// The stored value is the 'luggage' and the id of the item is the 'claim check'
    /// </summary>
    public interface IAmAStorageProvider
    {
        /// <summary>
        /// Delete the luggage identified by the claim check
        /// Used to clean up after luggage is retrieved
        /// </summary>
        /// <param name="claimCheck">The claim check for the luggage</param>
        void Delete(string claimCheck);


        /// <summary>
        /// Downloads the luggage associated with the claim check
        /// </summary>
        /// <param name="claimCheck">The claim check for the luggage</param>
        /// <returns>The luggage as a stream</returns>
        Stream Retrieve(string claimCheck);

        /// <summary>
        /// Do we have luggage for this claim check - in case of error or deletion
        /// </summary>
        /// <param name="claimCheck"></param>
        bool HasClaim(string claimCheck);

        /// <summary>
        /// Puts luggage into the store and provides a claim check for that luggage
        /// </summary>
        /// <param name="stream">A stream representing the luggage to check</param>
        /// <returns>A claim check for the luggage stored</returns>
        string Store(Stream stream);
    }
}
