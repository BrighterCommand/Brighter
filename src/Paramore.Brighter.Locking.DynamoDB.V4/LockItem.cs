#region Licence
/* The MIT License (MIT)
Copyright © 2024 Dominic Hickie <dominichickie@gmail.com>

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

using Amazon.DynamoDBv2.DataModel;

namespace Paramore.Brighter.Locking.DynamoDB.V4;

[DynamoDBTable("brighter_distributed_lock")]
public class LockItem
{
    /// <summary>
    /// The ID of the resource being locked
    /// </summary>
    [DynamoDBHashKey]
    [DynamoDBProperty]
    public string? ResourceId { get; init; }

    /// <summary>
    /// The time at which the lease for the lock expires, as a unix timestamp in milliseconds
    /// </summary>
    public long LeaseExpiry { get; init; }

    /// <summary>
    /// The ID of the lock that has been obtained
    /// </summary>
    public string? LockId { get; init; }
}