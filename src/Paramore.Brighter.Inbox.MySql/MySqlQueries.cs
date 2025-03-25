﻿#region Licence

/* The MIT License (MIT)
Copyright © 2025 Dominic Hickie <dominichickie@gmail.com>

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

namespace Paramore.Brighter.Inbox.MySql
{
    public class MySqlQueries : IRelationalDatabaseInboxQueries
    {
        public string AddCommand { get; } = "INSERT INTO {0} ([CommandID], [CommandType], [CommandBody], [Timestamp], [ContextKey]) VALUES (@CommandID, @CommandType, @CommandBody, @Timestamp, @ContextKey)";

        public string ExistsCommand { get; } = "SELECT [CommandID] FROM {0} WHERE [CommandID] = @CommandID AND [ContextKey] = @ContextKey LIMIT 1";

        public string GetCommand { get; } = "SELECT * FROM {0} where [CommandID] = @CommandID AND [ContextKey] = @ContextKey";
    }
}
