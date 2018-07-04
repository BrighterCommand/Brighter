#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.MessageStore.DynamoDB
{
    /// <summary>
    /// Class DynamoDbStoreConfiguration
    /// </summary>
    public class DynamoDbStoreConfiguration
    {
        /// <summary>
        /// Gets the table name
        /// </summary>
        /// <value>The table name</value>
        public string TableName { get; }
        /// <summary>
        /// Gets whether to use strongly consistent reads
        /// </summary>
        /// <value>Whether to use stronly consistent reads</value>
        public bool UseStronglyConsistentRead { get; }

        public string MessageIdIndex { get; }

        /// <summary>
        /// Initalises a new instance of the <see cref="DynamoDbStoreConfiguration"/> class.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="useStronglyConsistentRead">Whether to use strongly consistent reads.</param>
        public DynamoDbStoreConfiguration(string tableName, bool useStronglyConsistentRead, string messageIdIndex)
            => (TableName, UseStronglyConsistentRead, MessageIdIndex) = (tableName, useStronglyConsistentRead, messageIdIndex);
    }     
}
